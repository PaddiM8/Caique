using System.Diagnostics;
using Caique.Analysis;
using Caique.Parsing;
using Caique.Scope;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmTypeBuilder(LLVMContextRef llvmContext)
{
    private readonly LLVMContextRef _context = llvmContext;
    private readonly Dictionary<IDataType, LLVMTypeRef> _generalCache = [];
    private readonly Dictionary<FunctionSymbol, LLVMTypeRef> _unnamedFunctionCache = [];
    private readonly Dictionary<StructureSymbol, LLVMTypeRef> _namedStructCache = [];
    private readonly Dictionary<StructureSymbol, LLVMTypeRef> _unnamedStructCache = [];
    private readonly Dictionary<StructureSymbol, LLVMTypeRef> _vtableCache = [];
    private readonly Dictionary<StructureSymbol, LLVMTypeRef> _typeTableCache = [];

    public LLVMTypeRef BuildType(IDataType dataType)
    {
        if (_generalCache.TryGetValue(dataType, out var llvmType))
            return llvmType;

        var built = dataType switch
        {
            PrimitiveDataType primitive => Build(primitive),
            SliceDataType slice => Build(slice),
            FunctionDataType function => Build(function),
            StructureDataType structure => Build(structure),
            EnumDataType @enum => Build(@enum),
            _ => throw new NotImplementedException(),
        };

        _generalCache[dataType] = built;

        return built;
    }

    public LLVMTypeRef BuildInitType(SemanticInitNode node)
    {
        var parameterTypes = node.Parameters
            .Select(x => x.DataType)
            .Select(BuildType)
            .Prepend(LLVMTypeRef.CreatePointer(_context.VoidType, 0))
            .ToArray();

        return LLVMTypeRef.CreateFunction(_context.VoidType, parameterTypes);
    }

    public LLVMTypeRef BuildFunctionType(FunctionSymbol symbol)
    {
        if (_unnamedFunctionCache.TryGetValue(symbol, out var llvmType))
            return llvmType;

        var parameterTypes = symbol.SemanticDeclaration!.Parameters
            .Select(x => x.DataType)
            .Select(BuildType)
            .ToList();
        var returnType = BuildType(symbol.SemanticDeclaration.ReturnType);

        if (!symbol.SyntaxDeclaration.IsStatic)
        {
            var parentSymbol = ((ISyntaxStructureDeclaration)symbol.SyntaxDeclaration.Parent!).Symbol!;
            var parentType = LLVMTypeRef.CreatePointer(_context.VoidType, 0);
            parameterTypes.Insert(0, parentType);
        }

        var value = LLVMTypeRef.CreateFunction(returnType, parameterTypes.ToArray());
        _unnamedFunctionCache[symbol] = value;

        return value;
    }

    public LLVMTypeRef BuildStructType(StructureDataType dataType)
    {
        if (_unnamedStructCache.TryGetValue(dataType.Symbol, out var llvmType))
            return llvmType;

        var fields = dataType.IsClass()
            ? ((SemanticClassDeclarationNode)dataType.Symbol.SemanticDeclaration!).GetAllMemberFields()
            : dataType.Symbol.SemanticDeclaration!.Fields;
        var fieldTypes = fields
            .Where(x => !x.IsStatic)
            .Select(x => BuildType(x.DataType))
            .Prepend(BuildTypeTableType(dataType.Symbol))
            .ToArray();

        var value = _context.GetStructType(fieldTypes, Packed: false);
        _unnamedStructCache[dataType.Symbol] = value;

        return value;
    }

    public LLVMTypeRef BuildNamedStructType(StructureDataType dataType)
    {
        if (_namedStructCache.TryGetValue(dataType.Symbol, out var llvmType))
            return llvmType;

        var fields = dataType.IsClass()
            ? ((SemanticClassDeclarationNode)dataType.Symbol.SemanticDeclaration!).GetAllMemberFields()
            : dataType.Symbol.SemanticDeclaration!.Fields;
        var fieldTypes = fields
            .Where(x => !x.IsStatic)
            .Select(x => BuildType(x.DataType))
            .Prepend(BuildTypeTableType(dataType.Symbol))
            .ToArray();

        var value = _context.CreateNamedStruct($"class.{dataType}");
        value.StructSetBody(fieldTypes, Packed: false);

        _namedStructCache[dataType.Symbol] = value;

        return value;
    }

    public LLVMTypeRef BuildVtableType(StructureSymbol symbol)
    {
        if (_vtableCache.TryGetValue(symbol, out var existingType))
            return existingType;

        var functionTypes = symbol
            .SemanticDeclaration!
            .Functions
            .Select(x => BuildType(new FunctionDataType(x.Symbol)))
            .ToArray();

        var dataType = new StructureDataType(symbol);
        var vtableType = _context.CreateNamedStruct($"vtable.{dataType}");
        vtableType.StructSetBody(functionTypes, Packed: false);

        _vtableCache[symbol] = vtableType;

        return vtableType;
    }

    public LLVMTypeRef BuildTypeTableType(StructureSymbol symbol)
    {
        if (_typeTableCache.TryGetValue(symbol, out var existingType))
            return existingType;

        var types = new LLVMTypeRef[]
        {
            BuildVtableType(symbol),
        };

        var dataType = new StructureDataType(symbol);
        var typeTableType = _context.CreateNamedStruct($"typeTable.{dataType}");
        typeTableType.StructSetBody(types, Packed: false);

        _typeTableCache[symbol] = typeTableType;

        return typeTableType;
    }

    private LLVMTypeRef Build(PrimitiveDataType dataType)
    {
        return dataType.Kind switch
        {
            Primitive.Void => _context.VoidType,
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

    private LLVMTypeRef Build(SliceDataType dataType)
    {
        return LLVMTypeRef.CreatePointer(BuildType(dataType.SubType), 0);
    }

    private LLVMTypeRef Build(FunctionDataType dataType)
    {
        var function = BuildFunctionType(dataType.Symbol);

        return LLVMTypeRef.CreatePointer(function, 0);
    }

    private LLVMTypeRef Build(StructureDataType dataType)
    {
        if (dataType.Symbol.SemanticDeclaration is SemanticClassDeclarationNode)
        {
            return LLVMTypeRef.CreatePointer(BuildStructType(dataType), 0);
        }
        else if (dataType.Symbol.SemanticDeclaration is SemanticProtocolDeclarationNode)
        {
            var internalFatPointerTypes = new LLVMTypeRef[]
            {
                LLVMTypeRef.CreatePointer(BuildStructType(dataType), 0),
                LLVMTypeRef.CreatePointer(BuildVtableType(dataType.Symbol), 0),
            };

            return _context.GetStructType(internalFatPointerTypes, Packed: false);
        }

        Debug.Assert(dataType.Symbol.SemanticDeclaration != null);

        return BuildNamedStructType(dataType);
    }

    private LLVMTypeRef Build(EnumDataType dataType)
    {
        return BuildType(dataType.Symbol.SemanticDeclaration!.MemberDataType);
    }
}
