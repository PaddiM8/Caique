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
            var rootEnvironment = new ModuleEnvironment("root");
            CreateModuleEnvironment(path, rootEnvironment);

            return rootEnvironment;
        }

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