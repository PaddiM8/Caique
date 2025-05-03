using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class FieldSymbol(SyntaxFieldDeclarationNode declarationNode) : ISymbol
{
    public string Name
        => SyntaxDeclaration.Identifier.Value;

    public SyntaxFieldDeclarationNode SyntaxDeclaration { get; } = declarationNode;

    public SemanticFieldDeclarationNode? SemanticDeclaration { get; set; }
}
