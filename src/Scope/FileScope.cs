using Caique.Parsing;

namespace Caique.Scope;

public class FileScope(string name, string filePath, NamespaceScope namespaceScope) : IScope
{
    public string Name { get; } = name;

    public string FilePath { get; } = filePath;

    public IScope? Parent { get; } = namespaceScope;

    public NamespaceScope Namespace
        => namespaceScope;

    public IReadOnlyList<NamespaceScope> ImportedNamespaces
        => _importedNamespaces;

    public SyntaxTree? SyntaxTree { get; set; }

    private readonly List<NamespaceScope> _importedNamespaces = [];

    public void ImportNamespace(NamespaceScope namespaceScope)
    {
        _importedNamespaces.Add(namespaceScope);
    }

    public bool ImportNamespace(List<string> path)
    {
        var namespaceScope = Namespace.ResolveNamespace(path);
        if (namespaceScope == null)
            return false;

        _importedNamespaces.Add(namespaceScope);
        return true;
    }

    public ISymbol? ResolveSymbol(List<string> typeNames)
    {
        var localStructure = Namespace.ResolveSymbol(typeNames);
        if (localStructure != null)
            return localStructure;

        foreach (var importedNamespace in ImportedNamespaces)
        {
            var importedStructure = importedNamespace.ResolveSymbol(typeNames);
            if (importedStructure != null)
                return importedStructure;
        }

        return null;
    }

    public StructureSymbol? ResolveStructure(List<string> typeNames)
    {
        return ResolveSymbol(typeNames) as StructureSymbol;
    }
}
