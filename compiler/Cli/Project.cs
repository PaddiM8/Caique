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
        private FileInfo? _projectFile;
        private readonly HashSet<string> _ignoredDirectories = new HashSet<string>
        {
            "target"
        };

        public static Project Load(string projectFilePath)
        {
            return new Project
            {
                _projectFile = new FileInfo(projectFilePath)
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
                _projectFile = new FileInfo(projectFilePath)
            };
        }

        /// <summary>
        /// Compile the project.
        /// </summary>
        public void Build(BuildOptions options)
        {
            string projectPath = _projectFile!.Directory!.FullName;
            var (environment, sourcePath, targetPath) = PrepareBuild(projectPath);

            var compilation = new Compilation(environment, sourcePath)
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
            string projectPath = _projectFile!.Directory!.FullName;
            var (environment, sourcePath, targetPath) = PrepareBuild(projectPath);
            var compilation = new Compilation(environment, sourcePath);

            compilation.Compile(targetPath);
            if (LinkObjectFiles(targetPath, options.StdPath))
                // Run the generated executable
                Process.Start(targetPath + "/main");
        }

        private static bool LinkObjectFiles(string targetPath, string stdPath)
        {
            var objectFiles = Directory.GetFiles(targetPath, "*.o").ToList();

            var absoluteStdPath = Path.Combine(Environment.CurrentDirectory, stdPath);
            if (!Directory.Exists(absoluteStdPath))
            {
                Console.WriteLine("Standard library could not be found.");
                return false;
            }

            objectFiles.AddRange(Directory.GetFiles(absoluteStdPath));

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gcc",
                    Arguments = "-o main " + string.Join(" ", objectFiles),
                    WorkingDirectory = targetPath,
                }
            };
            process.Start();
            process.WaitForExit();

            return process.ExitCode == 0;
        }

        private (ModuleEnvironment, string sourcePath, string targetPath)
            PrepareBuild(string projectPath)
        {
            string sourcePath = $"{projectPath}/src";
            string targetPath = $"{projectPath}/target";

            // TODO: Cache
            foreach (var targetFile in Directory.GetFiles(targetPath))
                File.Delete(targetFile);

            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            return (CreateModuleEnvironment(sourcePath), sourcePath, targetPath);
        }

        /// <summary>
        /// Scan the directory structure and build a module tree based of it.
        /// </summary>
        /// <param name="path">Path to the soruce directory.</param>
        /// <returns>Module tree.</returns>
        private ModuleEnvironment CreateModuleEnvironment(string path)
        {
            var rootEnvironment = new ModuleEnvironment("root");
            CreateModuleEnvironment(path, rootEnvironment);

            return rootEnvironment;
        }

        /// <summary>
        /// Scan the directory structure and build a module tree based of it.
        /// </summary>
        /// <param name="path">Path to the soruce directory.</param>
        /// <returns>Module tree.</returns>
        private ModuleEnvironment CreateModuleEnvironment(string path, ModuleEnvironment environment)
        {
            var directories = Directory.GetDirectories(path);
            bool hasSubDirectory = false;
            foreach (var directoryPath in directories)
            {
                string identifier = Path.GetFileName(directoryPath)!;
                if (identifier.StartsWith(".") ||
                    _ignoredDirectories.Contains(identifier)) continue;

                hasSubDirectory = true;

                environment = CreateModuleEnvironment(
                    directoryPath,
                    environment.CreateChildModule(identifier)
                );
            }

            if (hasSubDirectory) environment = environment.Parent!;

            foreach (var filePath in Directory.GetFiles(path))
            {
                if (!filePath.EndsWith(".cq")) continue;
                string identifier = Path.GetFileNameWithoutExtension(filePath);
                environment.CreateChildModule(identifier, filePath);
            }

            return environment;
        }
    }
}