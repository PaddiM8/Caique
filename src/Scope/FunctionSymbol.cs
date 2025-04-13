using Caique.Parsing;

namespace Caique.Scope;

public class FunctionSymbol(SyntaxFunctionDeclarationNode declarationNode) : ISymbol
{
    public string Name
        => Declaration.Identifier.Value;

    public SyntaxFunctionDeclarationNode Declaration { get; } = declarationNode;
}
