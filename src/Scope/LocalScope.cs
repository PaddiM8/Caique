namespace Caique.Scope;

public class LocalScope(IScope parent) : IScope
{
    public IScope Parent { get; } = parent;

    public NamespaceScope Namespace
        => Parent.Namespace;

    public StructureScope Structure
        => Parent is LocalScope localParent
            ? localParent.Structure
            : (StructureScope)Parent;

    private readonly Dictionary<string, VariableSymbol> _variables = [];

    public void AddSymbol(VariableSymbol symbol)
    {
        _variables[symbol.Name] = symbol;
    }

    public VariableSymbol? FindSymbol(string name)
    {
        if (_variables.TryGetValue(name, out var symbol))
            return symbol;

        return (Parent as LocalScope)?.FindSymbol(name);
    }
}
