using System;
using System.IO;
using Caique.Semantics;

namespace Caique
{
    /// <summary>
    /// Manager for a specific Caique project.
    /// It reads the project file, and will for example
    /// build the module tree and build the project.
    /// </summary>
    public class ProjectManager
    {
        private readonly FileInfo _projectFile;

        public ProjectManager(string projectFilePath)
        {
            _projectFile = new FileInfo(projectFilePath);
        }

        /// <summary>
        /// Compile the project.
        /// </summary>
        public void Build()
        {
            string projectPath = _projectFile.Directory.FullName;
            string sourcePath = $"{projectPath}/src";

            var environment = CreateModuleEnvironment(sourcePath);
            new Compilation(environment, sourcePath);
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