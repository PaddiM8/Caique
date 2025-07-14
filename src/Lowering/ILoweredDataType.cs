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

    public ILoweredDataType Dereference()
        => InnerType;
}

public class LoweredPrimitiveDataType(Primitive primitive) : ILoweredDataType
{
    public Primitive Primitive { get; } = primitive;
}

public class LoweredSliceDataType(ILoweredDataType innerType) : ILoweredDataType
{
    public ILoweredDataType InnerType { get; } = innerType;
}

public class LoweredStructDataType(List<ILoweredDataType> fieldTypes, string? name) : ILoweredDataType
{
    public string? Name { get; } = name;

    public List<ILoweredDataType> FieldTypes { get; } = fieldTypes;

    public bool IsString()
        => Name == "std:prelude:String";
}

public class LoweredFunctionDataType(List<ILoweredDataType> parameterTypes, ILoweredDataType returnType) : ILoweredDataType
{
    public List<ILoweredDataType> ParameterTypes { get; } = parameterTypes;

    public ILoweredDataType ReturnType { get; } = returnType;
}
