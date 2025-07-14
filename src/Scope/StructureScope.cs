namespace Caique.Scope;

public class StructureScope(NamespaceScope parentNamespace) : IScope
{
    public IScope Parent
        => parentNamespace;

    public NamespaceScope Namespace
        => parentNamespace;

    private readonly Dictionary<string, ISymbol> _symbols = [];
    private readonly Dictionary<string, TypeSymbol> _typeParameters = [];

    public void AddSymbol(ISymbol symbol)
    {
        _symbols[symbol.Name] = symbol;
    }

    public ISymbol? FindSymbol(string name)
    {
        _symbols.TryGetValue(name, out var symbol);

        return symbol;
    }

    public void AddTypeParameter(TypeSymbol symbol)
    {
        _typeParameters[symbol.Name] = symbol;
    }

    public TypeSymbol? FindTypeSymbol(string name)
    {
        _typeParameters.TryGetValue(name, out var symbol);

        return symbol;
    }
}
