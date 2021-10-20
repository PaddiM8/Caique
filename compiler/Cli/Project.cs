using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Caique.Cli.Options;
using Caique.Semantics;
using Microsoft.Extensions.FileProviders;
using Nett;

namespace Caique.Cli
{
    /// <summary>
    /// Manager for a specific Caique project.
    /// It reads the project file, and will for example
    /// build the module tree and build the project.
    /// </summary>
    public class Project
    {
        private FileInfo? _projectFileInfo;
        private ProjectFile? _projectFile;

        public static Project Load(string projectFilePath)
        {
            var projectFile = Toml.ReadFile<ProjectFile>(projectFilePath);
            string programPath = Process.GetCurrentProcess().MainModule!.FileName!;
            projectFile.Dependencies.Add("core", Path.Combine(programPath, "std/core"));

            return new Project
            {
                _projectFileInfo = new FileInfo(projectFilePath),
                _projectFile = projectFile
            };
        }

        public static Project Create(string? name = null)
        {
            // Get the project directory (and create if needed).
            var projectDirectory = name == null
                ? new DirectoryInfo(Directory.GetCurrentDirectory())
                : Directory.CreateDirectory(name);

            name = projectDirectory.Name;
            var srcDirectory = projectDirectory.CreateSubdirectory("src");

            // Create the default file(s)
            File.WriteAllText(
                srcDirectory.FullName + "/main.cq",
                FileProvider.GetFileContent("main.cq")
            );

            // Create the project.toml file.
            string projectFilePath = projectDirectory.FullName + "/project.toml";
            Toml.WriteFile(
                new ProjectFile
                {
                    Name = name,
                    Author = "Anonymous",
                    Version = "1.0.0",
                },
                projectFilePath
            );

            return new Project
            {
                _projectFileInfo = new FileInfo(projectFilePath)
            };
        }

        /// <summary>
        /// Compile the project.
        /// </summary>
        public void Build(BuildOptions options)
        {
            string projectPath = _projectFileInfo!.Directory!.FullName;
            var (sourcePath, targetPath) = PrepareBuild(projectPath, options.StdPath);

            var compilation = new Compilation(sourcePath, _projectFile!.Dependencies)
            {
                PrintTokens = options.PrintTokens,
                PrintAst = options.PrintAst,
                PrintEnvironment = options.PrintEnvironment
            };

            compilation.Compile(targetPath);
            LinkObjectFiles(targetPath, options.StdPath);
        }

        /// <summary>
        /// Run the project.
        /// </summary>
        public void Run(RunOptions options)
        {
            string projectPath = _projectFileInfo!.Directory!.FullName;
            var (sourcePath, targetPath) = PrepareBuild(projectPath, options.StdPath);
            var compilation = new Compilation(sourcePath, _projectFile!.Dependencies);

            compilation.Compile(targetPath);
            if (LinkObjectFiles(targetPath, options.StdPath))
                // Run the generated executable
                using (var process = new Process())
                {
                    process.StartInfo.FileName = targetPath + "/main";
                    process.Start();
                    process.WaitForExit();
                }
        }

        private static bool LinkObjectFiles(string targetPath, string stdPath)
        {
            var objectFiles = Directory.GetFiles(targetPath, "*.o").ToList();

            var llvmStdPath = Path.Combine(Environment.CurrentDirectory, stdPath, "llvm/bin");
            if (!Directory.Exists(llvmStdPath))
            {
                Console.WriteLine("Standard library could not be found.");
                return false;
            }

            objectFiles.AddRange(Directory.GetFiles(llvmStdPath));

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gcc",
                    Arguments = "-no-pie -o main " + string.Join(" ", objectFiles),
                    WorkingDirectory = targetPath,
                }
            };
            process.Start();
            process.WaitForExit();

            return process.ExitCode == 0;
        }

        private (string sourcePath, string targetPath)
            PrepareBuild(string projectPath, string stdPath)
        {
            string sourcePath = $"{projectPath}/src";
            string targetPath = $"{projectPath}/target";

            if (!string.IsNullOrEmpty(stdPath))
                _projectFile!.Dependencies["core"] = Path.Combine(stdPath, "core");

            // TODO: Cache
            if (Directory.Exists(targetPath))
            {
                foreach (var targetFile in Directory.GetFiles(targetPath))
                    File.Delete(targetFile);
            }
            else
            {
                Directory.CreateDirectory(targetPath);
            }

            return (sourcePath, targetPath);
        }
    }
}