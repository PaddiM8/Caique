using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Caique.Analysis;
using Caique.Backend;
using Caique.Lexing;
using Caique.Scope;

namespace Caique.Lowering;

record TypeArgumentQueueEntry(
    IReadOnlyDictionary<TypeSymbol, IDataType> TypeArgumentLookup,
    SemanticNode Declaration
);

public class TypeArgumentResolver : ICloneable
{
    private readonly Stack<TypeArgumentQueueEntry> _typeArgumentStack;

    public TypeArgumentResolver()
    {
        _typeArgumentStack = [];
    }

    private TypeArgumentResolver(IEnumerable<TypeArgumentQueueEntry> entries)
    {
        _typeArgumentStack = new Stack<TypeArgumentQueueEntry>(entries);
    }

    public object Clone()
    {
        return new TypeArgumentResolver([.._typeArgumentStack]);
    }

    public IDataType Resolve(TypeSymbol typeSymbol)
    {
        foreach (var entry in _typeArgumentStack)
        {
            if (entry.TypeArgumentLookup.TryGetValue(typeSymbol, out var resolved))
                return resolved;
        }

        throw new InvalidOperationException();
    }

    public int PushAllFromOtherResolver(TypeArgumentResolver other)
    {
        foreach (var entry in other._typeArgumentStack)
            _typeArgumentStack.Push(entry);

        return other._typeArgumentStack.Count;
    }

    public void PushTypeArguments(IEnumerable<IDataType> typeArguments, ISemanticStructureDeclaration declaration)
    {
        var symbolToArgumentMap = declaration
            .Symbol
            .SyntaxDeclaration
            .TypeParameters
            .Select(x => x.Symbol)
            .Zip(typeArguments)
            .Where(x => x.First != (x.Second as TypeParameterDataType)?.Symbol)
            .ToDictionary(x => x.First, x => x.Second);
        _typeArgumentStack.Push(new TypeArgumentQueueEntry(symbolToArgumentMap, (SemanticNode)declaration));
    }

    public void PushTypeArguments(IEnumerable<IDataType> typeArguments, SemanticFunctionDeclarationNode declaration)
    {
        var symbolToArgumentMap = declaration
            .Symbol
            .SyntaxDeclaration
            .TypeParameters
            .Select(x => x.Symbol)
            .Zip(typeArguments)
            .Where(x => x.First != (x.Second as TypeParameterDataType)?.Symbol)
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
            .FirstOrDefault(x => x.Declaration is ISemanticStructureDeclaration)?
            .TypeArgumentLookup
            .Values
            .ToList() ?? [];
    }
}
