using Caique.Scope;

namespace Caique.Preprocessing;

public class Preprocessor
{
    public static Project Process(string projectName, string projectFilePath)
    {
        var project = new Project(projectName, projectFilePath);
        // TODO: Project files don't exist yet
        //var directoryPath = Path.GetDirectoryName(projectFilePath)!;
        var directoryPath = projectFilePath;
        var namespaceScope = BuildScope(projectName, directoryPath, null, project);
        project.Initialise(namespaceScope);

        return project;
    }

    private static NamespaceScope BuildScope(
        string name,
        string directoryPath,
        NamespaceScope? parent,
        Project project
    )
    {
        var scope = new NamespaceScope(name, directoryPath, parent, project);
        foreach (var filePath in Directory.GetFiles(directoryPath, "*.cq"))
        {
            string fileScopeName = Path.GetFileNameWithoutExtension(filePath);
            var fileContent = File.ReadAllText(filePath);
            var fileScope = new FileScope(fileScopeName, filePath, fileContent, scope);
            scope.AddScope(fileScope);
        }

        foreach (var childDirectoryPath in Directory.GetDirectories(directoryPath))
        {
            string? directoryName = new DirectoryInfo(childDirectoryPath).Name;
            if (directoryName == null)
            {
                continue;
            }

            var childScope = BuildScope(directoryName, childDirectoryPath, scope, project);
            scope.AddScope(childScope);
        }

        return scope;
    }
}
