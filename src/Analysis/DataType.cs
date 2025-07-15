using Caique.Parsing;
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
    Uint8,
    Uint16,
    Uint32,
    Uint64,
    ISize,
    USize,
    Uint128,
    Float16,
    Float32,
    Float64,
}

public static class PrimitiveExtensions
{
    public static int GetBitSize(this Primitive primitive)
    {
        return primitive switch
        {
            Primitive.Void => 0,
            Primitive.Bool => 1,
            Primitive.Int8 => 8,
            Primitive.Int16 => 16,
            Primitive.Int32 => 32,
            Primitive.Int64 => 64,
            Primitive.Int128 => 128,
            Primitive.Uint8 => 8,
            Primitive.Uint16 => 16,
            Primitive.Uint32 => 32,
            Primitive.Uint64 => 64,
            Primitive.Uint128 => 128,
            Primitive.Float16 => 16,
            Primitive.Float32 => 32,
            Primitive.Float64 => 64,
            Primitive.ISize => 64,
            Primitive.USize => 64,
        };
    }

    public static bool IsNumber(this Primitive primitive)
        => primitive >= Primitive.Int8 && primitive <= Primitive.Float64;

    public static bool IsInteger(this Primitive primitive)
        => primitive >= Primitive.Int8 && primitive <= Primitive.USize;

    public static bool IsSignedInteger(this Primitive primitive)
        => primitive >= Primitive.Int8 && primitive <= Primitive.ISize;

    public static bool IsUnsignedInteger(this Primitive primitive)
        => primitive >= Primitive.Uint8 && primitive <= Primitive.USize;

    public static bool IsFloat(this Primitive primitive)
        => primitive >= Primitive.Float16 && primitive <= Primitive.Float64;
}

public enum TypeEquivalence
{
    Incompatible,
    Identical,
    ImplicitCast,
}

public interface IDataType
{
    TypeEquivalence IsEquivalent(IDataType other);

    bool IsVoid()
        => false;

    bool IsBoolean()
        => false;

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

    bool IsString()
        => false;

    bool IsClass()
        => false;

    bool IsProtocol()
        => false;

    bool IsEnum()
        => false;
}

public class PrimitiveDataType(Primitive kind) : IDataType
{
    public static PrimitiveDataType Void { get; } = new PrimitiveDataType(Primitive.Void);

    public Primitive Kind { get; } = kind;

    public int BitSize
        => Kind.GetBitSize();

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
            Primitive.Uint8 => "u8",
            Primitive.Uint16 => "u16",
            Primitive.Uint32 => "u32",
            Primitive.Uint64 => "u64",
            Primitive.Uint128 => "u128",
            Primitive.Float16 => "f16",
            Primitive.Float32 => "f32",
            Primitive.Float64 => "f64",
            Primitive.ISize => "isize",
            Primitive.USize => "usize",
        };
    }

    public override int GetHashCode()
        => Kind.GetHashCode();

    public TypeEquivalence IsEquivalent(IDataType other)
    {
        if (other is PrimitiveDataType otherPrimitive && otherPrimitive.Kind == Kind)
            return TypeEquivalence.Identical;

        return TypeEquivalence.Incompatible;
    }

    public bool IsVoid()
        => Kind == Primitive.Void;

    public bool IsBoolean()
        => Kind == Primitive.Bool;

    public bool IsNumber()
        => Kind.IsNumber();

    public bool IsInteger()
        => Kind.IsInteger();

    public bool IsSignedInteger()
        => Kind.IsSignedInteger();

    public bool IsUnsignedInteger()
        => Kind.IsUnsignedInteger();

    public bool IsFloat()
        => Kind.IsFloat();
}

public class SliceDataType(IDataType subType) : IDataType
{
    public IDataType SubType { get; } = subType;

    public override string ToString()
        => $"[{SubType}]";

    public override int GetHashCode()
        => HashCode.Combine(typeof(SliceDataType), SubType);

    public TypeEquivalence IsEquivalent(IDataType other)
    {
        return other is SliceDataType otherSlice && SubType.IsEquivalent(otherSlice.SubType) == TypeEquivalence.Identical
            ? TypeEquivalence.Identical
            : TypeEquivalence.Incompatible;
    }
}

public class StructureDataType(StructureSymbol symbol) : IDataType
{
    public StructureSymbol Symbol { get; } = symbol;

    public override string ToString()
        => $"{Symbol.Namespace}:{Symbol.Name}";

    public override int GetHashCode()
        => Symbol.SyntaxDeclaration.GetHashCode();

    public TypeEquivalence IsEquivalent(IDataType other)
    {
        if (other is StructureDataType otherStructure)
        {
            if (otherStructure.Symbol == Symbol)
                return TypeEquivalence.Identical;

            if (Symbol.SyntaxDeclaration.SubTypes.Any(x => x.ResolvedSymbol == otherStructure.Symbol))
                return TypeEquivalence.ImplicitCast;
        }

        return TypeEquivalence.Incompatible;
    }

    public bool IsString()
        => Symbol is StructureSymbol { SyntaxDeclaration.Symbol: { Name: "String", Namespace.Name: "prelude" } };

    public bool IsClass()
        => Symbol is StructureSymbol { SyntaxDeclaration: SyntaxClassDeclarationNode };

    public bool IsProtocol()
        => Symbol is StructureSymbol { SyntaxDeclaration: SyntaxProtocolDeclarationNode };
}

public class FunctionDataType(FunctionSymbol symbol) : IDataType
{
    public FunctionSymbol Symbol { get; } = symbol;

    public override string ToString()
    {
        var parameters = Symbol
            .SyntaxDeclaration
            .Parameters
            .Select(x => x.Type.TypeNames.Select(t => t.Value))
            .Select(x => string.Join(":", x));
        var typeNames = Symbol
            .SyntaxDeclaration
            .ReturnType?
            .TypeNames
            .Select(x => x.Value);
        var returnType = string.Join(":", typeNames ?? []);

        return $"Fn({string.Join(", ", parameters)})({returnType})";
    }

    public override int GetHashCode()
        => Symbol.SyntaxDeclaration.GetHashCode();

    public TypeEquivalence IsEquivalent(IDataType other)
    {
        return other is FunctionDataType otherFunction && otherFunction.Symbol == Symbol
            ? TypeEquivalence.Identical
            : TypeEquivalence.Incompatible;
    }
}

public class EnumDataType(EnumSymbol symbol) : IDataType
{
    public EnumSymbol Symbol { get; } = symbol;

    public override string ToString()
        => $"{Symbol.Namespace}:{Symbol.SyntaxDeclaration.Identifier.Value}";

    public override int GetHashCode()
        => Symbol.SyntaxDeclaration.GetHashCode();

    public TypeEquivalence IsEquivalent(IDataType other)
    {
        if (other is EnumDataType otherDataType && Symbol == otherDataType.Symbol)
            return TypeEquivalence.Identical;

        return TypeEquivalence.Incompatible;
    }

    public bool IsEnum()
    {
        return true;
    }
}
