using System;
using System.IO;
using Caique.CLI.Options;
using Caique.Semantics;
using Microsoft.Extensions.FileProviders;
using Nett;

namespace Caique.CLI
{
    /// <summary>
    /// Manager for a specific Caique project.
    /// It reads the project file, and will for example
    /// build the module tree and build the project.
    /// </summary>
    public class Project
    {
        private FileInfo? _projectFile;

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
        public void Build(BuildOptions buildOptions)
        {
            string projectPath = _projectFile!.Directory.FullName;
            string sourcePath = $"{projectPath}/src";

            var environment = CreateModuleEnvironment(sourcePath);
            var compilation = new Compilation(environment, sourcePath)
            {
                PrintTokens = buildOptions.PrintTokens,
                PrintAst = buildOptions.PrintAst,
                PrintEnvironment = buildOptions.PrintEnvironment
            };

            compilation.Compile();
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
            foreach (var directoryPath in directories)
            {
                string identifier = Path.GetFileName(directoryPath)!;
                environment = CreateModuleEnvironment(
                    directoryPath,
                    environment.CreateChildModule(identifier)
                );
            }

            if (directories.Length > 0)
                environment = environment.Parent!;

            foreach (var filePath in Directory.GetFiles(path))
            {
                string identifier = Path.GetFileNameWithoutExtension(filePath);
                environment.CreateChildModule(identifier, filePath);
            }

            return environment;
        }
    }
}