using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class StructureSymbol(string name, ISyntaxStructureDeclaration node, NamespaceScope namespaceNode) : ISymbol
{
    public string Name { get; } = name;

    public ISyntaxStructureDeclaration SyntaxDeclaration { get; } = node;

    public ISemanticStructureDeclaration? SemanticDeclaration { get; set; }

    public NamespaceScope Namespace { get; } = namespaceNode;

    public IEnumerable<List<IDataType>> TypeArgumentOccurrences
        => _typeArgumentOccurrences.Select(x => x.TypeArguments);

    private readonly HashSet<TypeArgumentList> _typeArgumentOccurrences = [];

    public void AddTypeArgumentOccurrence(List<IDataType> typeArguments)
    {
        _typeArgumentOccurrences.Add(new TypeArgumentList(typeArguments));
   }
}

public class TypeArgumentList(List<IDataType> typeArguments)
{
    public List<IDataType> TypeArguments { get; } = typeArguments;

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach (var typeArgument in TypeArguments)
            hashCode.Add(typeArgument.GetHashCode());

        return hashCode.ToHashCode();
    }
}
