using Caique.Parsing;

namespace Caique.Scope;

public class StructureSymbol(string name, SyntaxNode node, NamespaceScope namespaceNode) : ISymbol
{
    public string Name { get; } = name;

    public SyntaxNode Node { get; } = node;

    public NamespaceScope Namespace { get; } = namespaceNode;
}
