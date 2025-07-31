using Caique.Analysis;

namespace Caique.Lowering;

public interface ILoweredDataType
{
    ILoweredDataType Dereference()
        => throw new InvalidOperationException();

    bool IsString()
        => false;
}

public class LoweredPointerDataType(ILoweredDataType innerType) : ILoweredDataType
{
    public ILoweredDataType InnerType { get; } = innerType;

    public override string ToString()
        => InnerType.ToString()!;

    public ILoweredDataType Dereference()
        => InnerType;
}

public class LoweredPrimitiveDataType(Primitive primitive) : ILoweredDataType
{
    public Primitive Primitive { get; } = primitive;

    public override string ToString()
        => Primitive.ToString()!;
}

public class LoweredSliceDataType(ILoweredDataType innerType) : ILoweredDataType
{
    public ILoweredDataType InnerType { get; } = innerType;

    public override string ToString()
        => $"[{InnerType}]";
}

public class LoweredStructDataType(List<ILoweredDataType> fieldTypes, string? name) : ILoweredDataType
{
    public string? Name { get; } = name;

    public List<ILoweredDataType> FieldTypes { get; } = fieldTypes;

    public override string ToString()
        => Name ?? "{" + string.Join(",", FieldTypes) + "}";

    public bool IsString()
        => Name == "std:prelude:String";
}

public class LoweredFunctionDataType(List<ILoweredDataType> parameterTypes, ILoweredDataType returnType) : ILoweredDataType
{
    public List<ILoweredDataType> ParameterTypes { get; } = parameterTypes;

    public ILoweredDataType ReturnType { get; } = returnType;

    public override string ToString()
        => "(" + string.Join(",", ParameterTypes) + ")" + ReturnType;
}
