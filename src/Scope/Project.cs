namespace Caique.Scope;

public class Project(string name, string projectFilePath)
{
    public string Name { get; private set; } = name;

    public string ProjectFilePath { get; private set; } = projectFilePath;

    public NamespaceScope? ProjectNamespace { get; private set; }

    public Dictionary<string, Project> Dependencies { get; set; } = [];

    public void Initialise(NamespaceScope namespaceScope)
    {
        if (ProjectNamespace != null)
            throw new InvalidOperationException("Project has already been initialised.");

        ProjectNamespace = namespaceScope;
    }

    public NamespaceScope? ResolveNamespace(List<string> path)
    {
        if (path.Count == 0)
            return null;

        if (ProjectNamespace?.Name == path.First())
        {
            return path.Count == 1
                ? ProjectNamespace
                : ProjectNamespace.ResolveNamespace(path[1..]);
        }

        if (Dependencies.TryGetValue(path.First(), out var dependency))
        {
            return dependency.ResolveNamespace(path);
        }

        return null;
    }

    public StructureSymbol? ResolveType(List<string> typeNames)
    {
        if (typeNames.Count == 0)
            return null;

        if (ProjectNamespace?.Name == typeNames.First())
            return ProjectNamespace.ResolveStructure(typeNames[1..]);

        if (Dependencies.TryGetValue(typeNames.First(), out var dependency))
            return dependency.ResolveType(typeNames);

        return null;
    }

    public void AddDependency(Project dependency)
    {
        Dependencies[dependency.Name] = dependency;
    }
}
