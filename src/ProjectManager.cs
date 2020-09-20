using System;
using System.IO;
using Caique.Semantics;

namespace Caique
{
    public class ProjectManager
    {
        private readonly FileInfo _projectFile;

        public ProjectManager(string projectFilePath)
        {
            _projectFile = new FileInfo(projectFilePath);
        }

        public void Build()
        {
            string projectPath = _projectFile.Directory.FullName;
            string sourcePath = $"{projectPath}/src";

            var environment = CreateModuleEnvironment(sourcePath);
            new Compilation(environment);
        }

        private ModuleEnvironment CreateModuleEnvironment(string path)
        {
            var rootEnvironment = new ModuleEnvironment("root"); // TODO: Replace root with project name
            CreateModuleEnvironment(path, rootEnvironment);

            return rootEnvironment;
        }

        private ModuleEnvironment CreateModuleEnvironment(string path, ModuleEnvironment environment)
        {
            foreach (var directoryPath in Directory.GetDirectories(path))
            {
                string identifier = Path.GetDirectoryName(directoryPath)!;
                environment = CreateModuleEnvironment(
                    directoryPath,
                    environment.CreateChildModule(identifier)
                );
            }

            foreach (var filePath in Directory.GetFiles(path))
            {
                string identifier = Path.GetFileNameWithoutExtension(filePath);
                environment.CreateChildModule(identifier, filePath);
            }

            return environment;
        }
    }
}