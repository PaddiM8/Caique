namespace Caique.Scope;

public class NamespaceScope(string name, string filePath, IScope? parent, Project project) : IScope
{
    public string Name { get; } = name;

    public string FilePath { get; } = filePath;

    public IScope? Parent { get; } = parent;

    public Project Project { get; } = project;

    public NamespaceScope Namespace
        => this;

    private readonly Dictionary<string, NamespaceScope> _namespaceScopes = [];
    private readonly Dictionary<string, FileScope> _fileScopes = [];
    private readonly Dictionary<string, StructureSymbol> _structureSymbols = [];

    public override string ToString()
    {
        if (Parent == null)
            return Name;

        return $"{Parent}:{Name}";
    }

    public void AddScope(NamespaceScope scope)
    {
        _namespaceScopes[scope.Name] = scope;
    }

    public void AddScope(FileScope scope)
    {
        _fileScopes[scope.Name] = scope;
    }

    public void AddSymbol(StructureSymbol symbol)
    {
        _structureSymbols[symbol.Name] = symbol;
    }

    public StructureSymbol? FindType(string name)
    {
        if (_structureSymbols.TryGetValue(name, out StructureSymbol? symbol))
            return symbol;

        return (Parent as NamespaceScope)?.FindType(name);
    }

    public NamespaceScope? ResolveNamespace(List<string> path)
    {
        if (_namespaceScopes.TryGetValue(path.First(), out var foundScope))
        {
            return path.Count == 1
                ? foundScope
                : foundScope.ResolveNamespace(path[1..]);
        }

        return Project.ResolveNamespace(path);
    }

    public StructureSymbol? ResolveStructure(List<string> typeNames)
    {
        if (typeNames.Count == 0)
            return null;

        if (typeNames.Count == 1)
            return FindType(typeNames.Single());

        if (_namespaceScopes.TryGetValue(typeNames.First(), out var foundScope))
            return foundScope.ResolveStructure(typeNames[1..]);

        return Project.ResolveType(typeNames);
    }

    public void Traverse(Action<FileScope> callback)
    {
        foreach (var child in _fileScopes.Values)
        {
            callback(child);
        }

        foreach (var child in _namespaceScopes.Values)
        {
            child.Traverse(callback);
        }
    }
}
