using Caique.Parsing;
using Caique.Scope;

namespace Caique.Analysis;

public enum Primitive
{
    Void,
    Bool,
    String,
    Int8,
    Int16,
    Int32,
    Int64,
    Int128,
    Float16,
    Float32,
    Float64,
}

public interface IDataType
{
    bool IsEquivalent(IDataType other);

    bool IsNumber()
        => false;

    bool IsInteger()
        => false;

    bool IsSignedInteger()
        => false;

    bool IsUnsignedInteger()
        => false;

    bool IsFloat()
        => false;
}

public class PrimitiveDataType(Primitive kind) : IDataType
{
    public Primitive Kind { get; } = kind;

    public override string ToString()
    {
        return Kind switch
        {
            Primitive.Void => "void",
            Primitive.Bool => "bool",
            Primitive.String => "string",
            Primitive.Int8 => "i8",
            Primitive.Int16 => "i16",
            Primitive.Int32 => "i32",
            Primitive.Int64 => "i64",
            Primitive.Int128 => "i128",
            Primitive.Float16 => "f16",
            Primitive.Float32 => "f32",
            Primitive.Float64 => "f64",
        };
    }

    public bool IsEquivalent(IDataType other)
        => other is PrimitiveDataType otherPrimitive && otherPrimitive.Kind == Kind;

    public bool IsNumber()
        => Kind >= Primitive.Int8 && Kind <= Primitive.Float64;

    public bool IsInteger()
        => Kind >= Primitive.Int8 && Kind <= Primitive.Int128;

    public bool IsSignedInteger()
        => Kind >= Primitive.Int8 && Kind <= Primitive.Int128;

    public bool IsFloat()
        => Kind >= Primitive.Float16 && Kind <= Primitive.Float64;

    public override int GetHashCode()
        => Kind.GetHashCode();
}

public class SliceDataType(IDataType subType) : IDataType
{
    public IDataType SubType { get; } = subType;

    public bool IsEquivalent(IDataType other)
        => other is SliceDataType otherSlice &&
            SubType.IsEquivalent(otherSlice.SubType);

    public override string ToString()
        => $"[{SubType}]";

    public override int GetHashCode()
        => HashCode.Combine(typeof(SliceDataType), SubType);
}

public class StructureDataType(StructureSymbol symbol) : IDataType
{
    public StructureSymbol Symbol { get; } = symbol;

    public override string ToString()
        => $"{Symbol.Namespace}::{Symbol.Name}";

    public override int GetHashCode()
        => Symbol.SyntaxDeclaration.GetHashCode();

    public bool IsClass()
        => Symbol is StructureSymbol { SyntaxDeclaration: SyntaxClassDeclarationNode };

    public bool IsEquivalent(IDataType other)
        => other is StructureDataType otherStructure && otherStructure.Symbol == Symbol;
}

public class FunctionDataType(FunctionSymbol symbol) : IDataType
{
    public FunctionSymbol Symbol { get; } = symbol;

    public override string ToString()
    {
        var parameters = Symbol.SyntaxDeclaration.Parameters
            .Select(x => x.Type.TypeNames)
            .Select(x => string.Join("::", x));
        var returnType = string.Join("::", Symbol.SyntaxDeclaration.ReturnType?.TypeNames ?? []);

        return $"Fn({string.Join(", ", parameters)})({returnType})";
    }

    public bool IsEquivalent(IDataType other)
        => other is FunctionDataType otherFunction && otherFunction.Symbol == Symbol;

    public override int GetHashCode()
        => Symbol.SyntaxDeclaration.GetHashCode();
}
