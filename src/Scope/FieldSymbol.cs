using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class FieldSymbol(SyntaxFieldDeclarationNode declarationNode) : ISymbol
{
    public string Name
        => Declaration.Identifier.Value;

    public SyntaxFieldDeclarationNode Declaration { get; } = declarationNode;
}
