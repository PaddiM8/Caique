using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class VariableSymbol(ISemanticVariableDeclaration declarationNode) : ISymbol
{
    public string Name
        => SemanticDeclaration.Identifier.Value;

    public ISemanticVariableDeclaration SemanticDeclaration { get; } = declarationNode;
}
