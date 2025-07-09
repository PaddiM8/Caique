using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class EnumSymbol(string name, SyntaxEnumDeclarationNode node, NamespaceScope namespaceNode) : ISymbol
{
    public string Name { get; } = name;

    public SyntaxEnumDeclarationNode SyntaxDeclaration { get; } = node;

    public SemanticEnumDeclarationNode? SemanticDeclaration { get; set; }

    public NamespaceScope Namespace { get; } = namespaceNode;
}
