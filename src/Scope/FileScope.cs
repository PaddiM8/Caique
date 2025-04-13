using Caique.Parsing;

namespace Caique.Scope;

public class FileScope(string name, string filePath, NamespaceScope namespaceScope) : IScope
{
    public string Name { get; } = name;

    public string FilePath { get; } = filePath;

    public IScope? Parent { get; } = namespaceScope;

    public NamespaceScope Namespace
        => namespaceScope;

    public SyntaxTree? SyntaxTree { get; set; }
}
