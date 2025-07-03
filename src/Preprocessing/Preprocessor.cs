using Caique.Scope;

namespace Caique.Preprocessing;

public class Preprocessor
{
    public static Project Process(string projectName, string projectFilePath, NamespaceScope? prelude)
    {
        var project = new Project(projectName, projectFilePath);
        // TODO: Project files don't exist yet
        //var directoryPath = Path.GetDirectoryName(projectFilePath)!;
        var directoryPath = projectFilePath;
        var namespaceScope = BuildScope(projectName, directoryPath, null, project, prelude);
        project.Initialise(namespaceScope);

        return project;
    }

    private static NamespaceScope BuildScope(
        string name,
        string directoryPath,
        NamespaceScope? parent,
        Project project,
        NamespaceScope? prelude
    )
    {
        var scope = new NamespaceScope(name, directoryPath, parent, project);
        foreach (var filePath in Directory.GetFiles(directoryPath, "*.cq"))
        {
            string fileScopeName = Path.GetFileNameWithoutExtension(filePath);
            var fileScope = new FileScope(fileScopeName, filePath, scope);
            scope.AddScope(fileScope);

            if (prelude != null)
                fileScope.ImportNamespace(prelude);
        }

        foreach (var childDirectoryPath in Directory.GetDirectories(directoryPath))
        {
            string? directoryName = new DirectoryInfo(childDirectoryPath).Name;
            if (directoryName == null)
            {
                continue;
            }

            var childScope = BuildScope(directoryName, childDirectoryPath, scope, project, prelude);
            scope.AddScope(childScope);
        }

        return scope;
    }
}
