using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Caique.Analysis;
using Caique.Backend;
using Caique.Lexing;
using Caique.Scope;

namespace Caique.Lowering;

record TypeArgumentQueueEntry(
    Dictionary<TypeSymbol, IDataType> TypeArgumentLookup,
    SemanticNode Declaration
);

class TypeArgumentResolver
{
    private readonly Stack<TypeArgumentQueueEntry> _typeArgumentStack = [];

    public IDataType Resolve(TypeSymbol typeSymbol)
    {
        foreach (var entry in _typeArgumentStack)
        {
            if (entry.TypeArgumentLookup.TryGetValue(typeSymbol, out var resolved))
                return resolved;
        }

        throw new InvalidOperationException();
    }

    public void PushTypeArguments(IEnumerable<IDataType> typeArguments, ISemanticStructureDeclaration declaration)
    {
            var symbolToArgumentMap = declaration
                .Symbol
                .SyntaxDeclaration
                .TypeParameters
                .Select(x => x.Symbol)
                .Zip(typeArguments)
                .ToDictionary(x => x.First, x => x.Second);
        _typeArgumentStack.Push(new TypeArgumentQueueEntry(symbolToArgumentMap, (SemanticNode)declaration));
    }

    public void PopTypeArguments()
    {
        _typeArgumentStack.Pop();
    }

    public List<IDataType> GetCurrentStructureTypeArguments()
    {
        return _typeArgumentStack
            .First(x => x.Declaration is ISemanticStructureDeclaration)
            .TypeArgumentLookup
            .Values
            .ToList();
    }
}
