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

        var fieldTypes = dataType
            .Symbol
            .SemanticDeclaration!
            .Fields
            .Select(x => BuildType(x.DataType))
            .ToArray();

        var structValue = _context.CreateNamedStruct($"class.{dataType.Symbol.SyntaxDeclaration.Identifier.Value}");
        structValue.StructSetBody(fieldTypes, Packed: false);

        _namedStructCache[dataType] = structValue;

        return structValue;
    }

    private LLVMTypeRef Build(PrimitiveDataType dataType)
    {
        unsafe
        {
            return dataType.Kind switch
            {
                Primitive.Void => LLVM.VoidType(),
                Primitive.Bool => LLVM.Int1Type(),
                Primitive.String => LLVM.PointerType(LLVM.Int8Type(), 0),
                Primitive.Int8 => LLVM.Int8Type(),
                Primitive.Int16 => LLVM.Int16Type(),
                Primitive.Int32 => LLVM.Int32Type(),
                Primitive.Int64 => LLVM.Int64Type(),
                Primitive.Int128 => LLVM.Int128Type(),
                Primitive.Float16 => LLVM.HalfType(),
                Primitive.Float32 => LLVM.FloatType(),
                Primitive.Float64 => LLVM.DoubleType(),
            };
        }
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
