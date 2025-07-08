using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class FunctionSymbol(SyntaxFunctionDeclarationNode declarationNode) : ISymbol
{
    public string Name
        => SyntaxDeclaration.Identifier.Value;

    public SyntaxFunctionDeclarationNode SyntaxDeclaration { get; } = declarationNode;

    public SemanticFunctionDeclarationNode? SemanticDeclaration { get; set; }

    public bool IsVirtual { get; set; }
}
