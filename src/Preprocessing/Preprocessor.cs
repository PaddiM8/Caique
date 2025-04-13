using Caique.Scope;

namespace Caique.Preprocessing;

public class Preprocessor
{
    public static Project Process(string projectName, string directoryPath)
    {
        var project = new Project();
        var namespaceScope = BuildScope(projectName, directoryPath, null, project);
        project.Initialise(namespaceScope);

        return project;
    }

    private static NamespaceScope BuildScope(string name, string directoryPath, NamespaceScope? parent, Project project)
    {
        var scope = new NamespaceScope(name, directoryPath, parent, project);
        foreach (var filePath in Directory.GetFiles(directoryPath, "*.cq"))
        {
            string fileScopeName = Path.GetFileNameWithoutExtension(filePath);
            scope.AddScope(new FileScope(fileScopeName, filePath, scope));
        }

        foreach (var childDirectoryPath in Directory.GetDirectories(directoryPath))
        {
            string? directoryName = Path.GetDirectoryName(childDirectoryPath);
            if (directoryName == null)
            {
                continue;
            }

            scope.AddScope(BuildScope(directoryName, childDirectoryPath, scope, project));
        }

        return scope;
    }
}
