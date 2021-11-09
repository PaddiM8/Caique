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
                PrintEnvironment = options.PrintEnvironment,
                PrintLlvm = options.PrintLlvm
            };

            compilation.Compile(targetPath);
            if (options.PrintLlvm)
            {
                LinkLlvmFiles(targetPath);
            }
            else
            {
                LinkObjectFiles(targetPath, options.ShowLinkerOutput);
            }
        }

        /// <summary>
        /// Run the project.
        /// </summary>
        public void Run(RunOptions options)
        {
            string projectPath = _projectFileInfo!.Directory!.FullName;
            var (sourcePath, targetPath) = PrepareBuild(projectPath, options.StdPath);
            var compilation = new Compilation(sourcePath, _projectFile!.Dependencies)
            {
                PrintTokens = options.PrintTokens,
                PrintAst = options.PrintAst,
                PrintEnvironment = options.PrintEnvironment,
                PrintLlvm = options.PrintLlvm
            };
            compilation.Compile(targetPath);

            if (options.PrintLlvm)
            {
                LinkLlvmFiles(targetPath);
            }
            else if (LinkObjectFiles(targetPath, options.ShowLinkerOutput))
                // Run the generated executable
                using (var process = new Process())
                {
                    process.StartInfo.FileName = targetPath + "/main";
                    process.Start();
                    process.WaitForExit();
                }
        }

        private static void LinkLlvmFiles(string targetPath)
        {
            var llvmFiles = Directory.GetFiles(targetPath, "*.ll").ToList();
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "llvm-link",
                    Arguments = "-S -o single.ll " + string.Join(" ", llvmFiles),
                    WorkingDirectory = targetPath
                }
            };
            process.Start();
            process.WaitForExit();
        }

        private static bool LinkObjectFiles(string targetPath, bool showOutput = false)
        {
            var objectFiles = Directory.GetFiles(targetPath, "*.o").ToList();
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gcc",
                    Arguments = "-no-pie -o main " + string.Join(" ", objectFiles),
                    WorkingDirectory = targetPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = !showOutput,
                    RedirectStandardError = !showOutput,
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