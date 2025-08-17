namespace Caique.Scope;

public class StructureScope(NamespaceScope parentNamespace) : IScope
{
    public IScope Parent
        => parentNamespace;

    public NamespaceScope Namespace
        => parentNamespace;

    private readonly Dictionary<string, ISymbol> _symbols = [];

    public void AddSymbol(ISymbol symbol)
    {
        _symbols[symbol.Name] = symbol;
    }

    public ISymbol? FindSymbol(string name)
    {
        _symbols.TryGetValue(name, out var symbol);

        return symbol;
    }

    public bool ContainsSymbol(string name)
    {
        return _symbols.ContainsKey(name);
    }
}
