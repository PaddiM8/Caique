namespace Caique.Scope;

public class TypeScope(TypeScope? parent)
{
    private TypeScope? Parent { get; } = parent;

    private readonly Dictionary<string, TypeSymbol> _typeSymbols = [];

    public void AddSymbol(TypeSymbol symbol)
    {
        _typeSymbols[symbol.Name] = symbol;
    }

    public TypeSymbol? FindSymbol(string name)
    {
        if (_typeSymbols.TryGetValue(name, out var symbol))
            return symbol;

        return Parent?.FindSymbol(name);
    }
}
