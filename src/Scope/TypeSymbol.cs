using Caique.Analysis;

namespace Caique.Scope;

public class TypeSymbol(string name, ISymbol? declarationSymbol) : ISymbol
{
    public string Name { get; } = name;

    public ISymbol? DeclarationSymbol { get; set; } = declarationSymbol;
}
