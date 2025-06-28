using System.Diagnostics;
using Caique.Analysis;
using Caique.Parsing;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmTypeBuilder(LLVMContextRef llvmContext)
{
    private readonly LLVMContextRef _context = llvmContext;
    private readonly Dictionary<IDataType, LLVMTypeRef> _cache = [];
    private readonly Dictionary<StructureDataType, LLVMTypeRef> _namedStructCache = [];

    public LLVMTypeRef BuildType(IDataType dataType)
    {
        if (_cache.TryGetValue(dataType, out var llvmType))
            return llvmType;

        var built = dataType switch
        {
            PrimitiveDataType primitive => Build(primitive),
            SliceDataType slice => Build(slice),
            FunctionDataType function => Build(function),
            StructureDataType structure => Build(structure),
            _ => throw new NotImplementedException(),
        };

        _cache[dataType] = built;

        return built;
    }

    public LLVMTypeRef BuildInitType(SemanticInitNode node)
    {
        var parameterTypes = node.Parameters
            .Select(x => x.DataType)
            .Select(BuildType)
            .ToList();
        var returnType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0);

        return LLVMTypeRef.CreateFunction(returnType, parameterTypes.ToArray());
    }

    public LLVMTypeRef BuildNamedStructType(StructureDataType dataType)
    {
        if (_namedStructCache.TryGetValue(dataType, out var llvmType))
            return llvmType;

        var fields = dataType.IsClass()
            ? ((SemanticClassDeclarationNode)dataType.Symbol.SemanticDeclaration!).GetAllFields()
            : dataType.Symbol.SemanticDeclaration!.Fields;
        var fieldTypes = fields
            .Select(x => BuildType(x.DataType))
            .ToArray();

        var structValue = _context.CreateNamedStruct($"class.{dataType.Symbol.SyntaxDeclaration.Identifier.Value}");
        structValue.StructSetBody(fieldTypes, Packed: false);

        _namedStructCache[dataType] = structValue;

        return structValue;
    }

    private LLVMTypeRef Build(PrimitiveDataType dataType)
    {
        return dataType.Kind switch
        {
            Primitive.Void => LLVMTypeRef.Void,
            Primitive.Bool => LLVMTypeRef.Int1,
            Primitive.String => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            Primitive.Int8 => LLVMTypeRef.Int8,
            Primitive.Int16 => LLVMTypeRef.Int16,
            Primitive.Int32 => LLVMTypeRef.Int32,
            Primitive.Int64 => LLVMTypeRef.Int64,
            Primitive.Int128 => LlvmUtils.Int128,
            Primitive.Float16 => LLVMTypeRef.Half,
            Primitive.Float32 => LLVMTypeRef.Float,
            Primitive.Float64 => LLVMTypeRef.Double,
        };
    }

    private LLVMTypeRef Build(SliceDataType dataType)
    {
        unsafe
        {
            return LLVM.PointerType(BuildType(dataType.SubType), 0);
        }
    }

    private LLVMTypeRef Build(FunctionDataType dataType)
    {
        var parameterTypes = dataType.Symbol.SemanticDeclaration!.Parameters
            .Select(x => x.DataType)
            .Select(BuildType)
            .ToList();
        var returnType = BuildType(dataType.Symbol.SemanticDeclaration.ReturnType);

        if (!dataType.Symbol.SyntaxDeclaration.IsStatic)
        {
            var parentSymbol = ((ISyntaxStructureDeclaration)dataType.Symbol.SyntaxDeclaration.Parent!).Symbol!;
            var parentType = BuildType(new StructureDataType(parentSymbol));
            parameterTypes.Insert(0, parentType);
        }

        return LLVMTypeRef.CreateFunction(returnType, parameterTypes.ToArray());
    }

    private LLVMTypeRef Build(StructureDataType dataType)
    {
        if (dataType.Symbol.SemanticDeclaration is SemanticClassDeclarationNode)
            return LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0);

        Debug.Assert(dataType.Symbol.SemanticDeclaration != null);

        return BuildNamedStructType(dataType);
    }
}
