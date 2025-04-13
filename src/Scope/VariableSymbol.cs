using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class VariableSymbol(ISemanticVariableDeclaration declarationNode) : ISymbol
{
    public string Name
        => Declaration.Identifier.Value;

    public ISemanticVariableDeclaration Declaration { get; } = declarationNode;
}
