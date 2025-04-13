using Caique.Scope;

namespace Caique.Analysis;

public enum Primitive
{
    Void,
    Bool,
    Int8,
    Int16,
    Int32,
    Int64,
    Int128,
    Float8,
    Float16,
    Float32,
    Float64,
    Float128,
}

public interface IDataType
{
    bool IsEquivalent(IDataType other);

    bool IsNumber()
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
            Primitive.Int8 => "i8",
            Primitive.Int16 => "i16",
            Primitive.Int32 => "i32",
            Primitive.Int64 => "i64",
            Primitive.Int128 => "i128",
            Primitive.Float8 => "f8",
            Primitive.Float16 => "f16",
            Primitive.Float32 => "f32",
            Primitive.Float64 => "f64",
            Primitive.Float128 => "f128",
        };
    }

    public bool IsEquivalent(IDataType other)
        => other is PrimitiveDataType otherPrimitive && otherPrimitive.Kind == Kind;

    public bool IsNumber()
        => Kind >= Primitive.Int8 && Kind <= Primitive.Float128;
}

public class StructureDataType(StructureSymbol symbol) : IDataType
{
    public StructureSymbol Symbol { get; } = symbol;

    public override string ToString()
        => $"{Symbol.Namespace}::{Symbol.Name}";

    public bool IsEquivalent(IDataType other)
        => other is StructureDataType otherStructure && otherStructure.Symbol == Symbol;
}

public class FunctionDataType(FunctionSymbol symbol) : IDataType
{
    public FunctionSymbol Symbol { get; } = symbol;

    public override string ToString()
    {
        var parameters = Symbol.Declaration.Parameters
            .Select(x => x.Type.TypeNames)
            .Select(x => string.Join("::", x));
        var returnType = string.Join("::", Symbol.Declaration.ReturnType?.TypeNames ?? []);

        return $"Fn({string.Join(", ", parameters)})({returnType})";
    }

    public bool IsEquivalent(IDataType other)
        => other is FunctionDataType otherFunction && otherFunction.Symbol == Symbol;
}
