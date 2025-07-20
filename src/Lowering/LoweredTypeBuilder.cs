using System.Diagnostics;
using Caique.Analysis;
using Caique.Scope;

namespace Caique.Lowering;

public class LoweredTypeBuilder
{
    private readonly Dictionary<IDataType, ILoweredDataType> _generalCache = [];
    private readonly Dictionary<StructureSymbol, LoweredStructDataType> _structCache = [];
    private readonly Dictionary<(ISemanticStructureDeclaration, ISemanticStructureDeclaration), LoweredStructDataType> _vtableCache = [];
    private readonly Dictionary<SemanticClassDeclarationNode, LoweredStructDataType> _typeTableCache = [];

    public ILoweredDataType BuildType(IDataType dataType)
    {
        if (_generalCache.TryGetValue(dataType, out var existing))
            return existing;

        var lowered = dataType switch
        {
            PrimitiveDataType primitiveDataType => Build(primitiveDataType),
            SliceDataType sliceDataType => Build(sliceDataType),
            StructureDataType structureDataType => Build(structureDataType),
            FunctionDataType functionDataType => Build(functionDataType),
            EnumDataType enumDataType => Build(enumDataType),
            _ => throw new NotImplementedException(),
        };
        _generalCache[dataType] = lowered;

        return lowered;
    }

    public LoweredFunctionDataType BuildInitType(ISemanticInstantiableStructureDeclaration structure)
    {
        var parameterTypes = structure
            .Init
            .Parameters
            .Select(x => x.DataType)
            .Select(BuildType)
            .Prepend(BuildType(new StructureDataType(structure.Symbol)))
            .ToList();
        var voidType = new LoweredPrimitiveDataType(Primitive.Void);

        return new LoweredFunctionDataType(parameterTypes, voidType);
    }

    public LoweredStructDataType BuildVtableType(ISemanticStructureDeclaration classNode)
    {
        return BuildVtableType(classNode, classNode);
    }

    public LoweredStructDataType BuildVtableType(ISemanticStructureDeclaration implementor, ISemanticStructureDeclaration implemented)
    {
        if (_vtableCache.TryGetValue((implementor, implemented), out var existing))
            return existing;

        var functionTypes = implementor
            .Functions
            .Where(x => !x.IsStatic)
            .Select(x => BuildType(new FunctionDataType(x.Symbol)))
            .ToList();
        var implementedName = new StructureDataType(implemented.Symbol);
        var implementorName = new StructureDataType(implementor.Symbol);
        var name = $"{implementedName}.vtable.{implementorName}";

        var structValue = new LoweredStructDataType(functionTypes, name);
        _vtableCache[(implementor, implemented)] = structValue;

        return structValue;
    }

    public LoweredStructDataType BuildTypeTableType(SemanticClassDeclarationNode node)
    {
        if (_typeTableCache.TryGetValue(node, out var existing))
            return existing;

        var vtableType = new LoweredPointerDataType(BuildVtableType(node));
        var name = $"typetable.{new StructureDataType(node.Symbol)}";
        var structValue = new LoweredStructDataType([vtableType], name);

        _typeTableCache[node] = structValue;

        return structValue;
    }

    public LoweredStructDataType BuildStructType(StructureSymbol symbol)
    {
        if (_structCache.TryGetValue(symbol, out var existing))
            return existing;

        var fieldTypes = new List<ILoweredDataType>();
        var loweredType = new LoweredStructDataType(fieldTypes, symbol.Name);
        _structCache[symbol] = loweredType;

        if (symbol.SemanticDeclaration is SemanticClassDeclarationNode classNode)
        {
            fieldTypes.Add(BuildTypeTableType(classNode));

            var memberTypes = classNode
                .GetAllMemberFields()
                .Select(x => BuildType(x.DataType));
            fieldTypes.AddRange(memberTypes);
        }
        else
        {
            var memberTypes = symbol
                .SemanticDeclaration!
                .Fields
                .Select(x => BuildType(x.DataType));
            fieldTypes.AddRange(memberTypes);
        }

        return loweredType;
    }

    public LoweredFunctionDataType BuildGetterType(SemanticFieldDeclarationNode field)
    {
        var fieldType = BuildType(field.DataType);
        List<ILoweredDataType> parameterTypes = [];
        if (!field.IsStatic)
        {
            var selfType = BuildStructType(((ISemanticStructureDeclaration)field.Parent!).Symbol);
            parameterTypes.Add(new LoweredPointerDataType(selfType));
        }

        return new LoweredFunctionDataType(parameterTypes, fieldType);
    }

    public LoweredFunctionDataType BuildSetterType(SemanticFieldDeclarationNode field)
    {
        var fieldType = BuildType(field.DataType);
        List<ILoweredDataType> parameterTypes = [fieldType];
        if (!field.IsStatic)
        {
            var selfType = BuildStructType(((ISemanticStructureDeclaration)field.Parent!).Symbol);
            parameterTypes.Insert(0, new LoweredPointerDataType(selfType));
        }

        var setterReturnType = new LoweredPrimitiveDataType(Primitive.Void);

        return new LoweredFunctionDataType(parameterTypes, setterReturnType);
    }

    public LoweredFunctionDataType BuildFunctionType(FunctionSymbol symbol)
    {
        var parameterTypes = symbol
            .SemanticDeclaration!
            .Parameters
            .Select(x => BuildType(x.DataType))
            .ToList();
        var returnType = BuildType(symbol.SemanticDeclaration.ReturnType);

        if (!symbol.SemanticDeclaration.IsStatic)
        {
            var voidPointerType = new LoweredPointerDataType(
                new LoweredPrimitiveDataType(Primitive.Void)
            );
            parameterTypes.Insert(0, voidPointerType);
        }

        return new LoweredFunctionDataType(parameterTypes, returnType);
    }

    private LoweredPrimitiveDataType Build(PrimitiveDataType dataType)
    {
        return new LoweredPrimitiveDataType(dataType.Kind);
    }

    private LoweredSliceDataType Build(SliceDataType dataType)
    {
        return new LoweredSliceDataType(BuildType(dataType.SubType));
    }

    private ILoweredDataType Build(StructureDataType dataType)
    {
        if (dataType.Symbol.SemanticDeclaration is SemanticClassDeclarationNode)
        {
            return new LoweredPointerDataType(BuildStructType(dataType.Symbol));
        }
        else if (dataType.Symbol.SemanticDeclaration is SemanticProtocolDeclarationNode)
        {
            var internalFatPointerTypes = new List<ILoweredDataType>
            {
                new LoweredPointerDataType(BuildStructType(dataType.Symbol)),
                new LoweredPointerDataType(BuildVtableType(dataType.Symbol.SemanticDeclaration)),
            };

            return new LoweredStructDataType(internalFatPointerTypes, name: null);
        }

        throw new NotImplementedException();
    }

    private ILoweredDataType Build(FunctionDataType dataType)
    {
        return new LoweredPointerDataType(BuildFunctionType(dataType.Symbol));
    }

    private ILoweredDataType Build(EnumDataType dataType)
    {
        return BuildType(dataType.Symbol.SemanticDeclaration!.MemberDataType);
    }
}
