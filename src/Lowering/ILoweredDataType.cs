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

public record LoweredStructDataTypeField(string Name, ILoweredDataType DataType);

public class LoweredStructDataType(List<LoweredStructDataTypeField> fields, string? name) : ILoweredDataType
{
    public string? Name { get; } = name;

    public List<LoweredStructDataTypeField> Fields { get; } = fields;

    public override string ToString()
    {
        var fieldTypes = Fields.Select(x => x.DataType);

        return Name ?? "{" + string.Join(",", fieldTypes) + "}";
    }

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
