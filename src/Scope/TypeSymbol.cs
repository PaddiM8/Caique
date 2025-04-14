using Caique.Parsing;

namespace Caique.Scope;

public class StructureSymbol(string name, ISyntaxStructure node, NamespaceScope namespaceNode) : ISymbol
{
    public string Name { get; } = name;

    public ISyntaxStructure Node { get; } = node;

    public NamespaceScope Namespace { get; } = namespaceNode;
}
