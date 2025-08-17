using Caique.Analysis;
using Caique.Parsing;

namespace Caique.Scope;

public class StructureSymbol(string name, ISyntaxStructureDeclaration node, NamespaceScope namespaceNode) : ISymbol
{
    public string Name { get; } = name;

    public ISyntaxStructureDeclaration SyntaxDeclaration { get; } = node;

    public ISemanticStructureDeclaration? SemanticDeclaration { get; set; }

    public NamespaceScope Namespace { get; } = namespaceNode;

    public List<StructureSymbol> Implementors { get; } = [];

    public IEnumerable<StructureSymbol> GetAllNestedImplementors()
    {
        foreach (var implementor in Implementors)
        {
            yield return implementor;

            foreach (var nested in implementor.GetAllNestedImplementors())
                yield return nested;
        }
    }
}
