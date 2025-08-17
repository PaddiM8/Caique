using Caique.Analysis;
using Caique.Lowering;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmTypeBuilder(LLVMContextRef llvmContext)
{
    private readonly LLVMContextRef _context = llvmContext;
    private readonly Dictionary<string, LLVMTypeRef> _namedStructs = [];

    public LLVMTypeRef BuildType(ILoweredDataType dataType)
    {
        return dataType switch
        {
            LoweredPointerDataType pointer => Build(pointer),
            LoweredPrimitiveDataType primitive => Build(primitive),
            LoweredSliceDataType slice => Build(slice),
            LoweredFunctionDataType function => Build(function),
            LoweredStructDataType structure => Build(structure),
            _ => throw new NotImplementedException(),
        };
    }

    public LLVMTypeRef Build(LoweredPointerDataType dataType)
    {
        var innerType = BuildType(dataType.InnerType);

        return LLVMTypeRef.CreatePointer(innerType, 0);
    }

    public LLVMTypeRef Build(LoweredPrimitiveDataType dataType)
    {
        return dataType.Primitive switch
        {
            Primitive.Void => _context.VoidType,
            Primitive.Null => LLVMTypeRef.CreatePointer(_context.VoidType, 0),
            Primitive.Bool => _context.Int1Type,
            Primitive.Int8 => _context.Int8Type,
            Primitive.Int16 => _context.Int16Type,
            Primitive.Int32 => _context.Int32Type,
            Primitive.Int64 => _context.Int64Type,
            Primitive.Int128 => LlvmUtils.Int128Type(_context),
            Primitive.Uint8 => _context.Int8Type,
            Primitive.Uint16 => _context.Int16Type,
            Primitive.Uint32 => _context.Int32Type,
            Primitive.Uint64 => _context.Int64Type,
            Primitive.Uint128 => LlvmUtils.Int128Type(_context),
            Primitive.Float16 => _context.HalfType,
            Primitive.Float32 => _context.FloatType,
            Primitive.Float64 => _context.DoubleType,
            Primitive.ISize => _context.Int64Type,
            Primitive.USize => _context.Int64Type,
        };
    }

    public LLVMTypeRef Build(LoweredSliceDataType dataType)
    {
        var innerType = BuildType(dataType.InnerType);

        return LLVMTypeRef.CreatePointer(innerType, 0);
    }

    public LLVMTypeRef Build(LoweredFunctionDataType dataType)
    {
        var returnType = BuildType(dataType.ReturnType);
        var parameters = dataType
            .ParameterTypes
            .Select(BuildType)
            .ToArray();

        return LLVMTypeRef.CreateFunction(returnType, parameters);
    }

    public LLVMTypeRef Build(LoweredStructDataType dataType)
    {
        var fieldTypes = new List<LLVMTypeRef>();
        foreach (var fieldType in dataType.Fields)
        {
            if (fieldType.IsPlaceholder)
                continue;

            if (fieldType.DataType is LoweredStructDataType)
            {
                fieldTypes.Add(LLVMTypeRef.CreatePointer(_context.VoidType, 0));
            }
            else
            {
                fieldTypes.Add(BuildType(fieldType.DataType));
            }
        }

        if (dataType.Name == null)
            return _context.GetStructType(fieldTypes.ToArray(), Packed: false);

        if (_namedStructs.TryGetValue(dataType.Name, out var existing))
            return existing;

        var namedStruct = _context.CreateNamedStruct(dataType.Name);
        namedStruct.StructSetBody(fieldTypes.ToArray(), Packed: false);
        _namedStructs[dataType.Name] = namedStruct;

        return namedStruct;
    }
}
