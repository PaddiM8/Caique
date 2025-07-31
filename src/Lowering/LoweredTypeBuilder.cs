using System.Diagnostics;
using Caique.Analysis;
using Caique.Scope;

namespace Caique.Lowering;

class LoweredTypeBuilder(TypeArgumentResolver typeArgumentResolver, NameMangler mangler)
{
    private readonly Dictionary<IDataType, ILoweredDataType> _generalCache = [];
    private readonly Dictionary<StructureSymbol, LoweredStructDataType> _structCache = [];
    private readonly Dictionary<(StructureDataType, StructureDataType), LoweredStructDataType> _vtableCache = [];
    private readonly Dictionary<StructureDataType, LoweredStructDataType> _typeTableCache = [];
    private readonly TypeArgumentResolver _typeArgumentResolver = typeArgumentResolver;
    private readonly NameMangler _mangler = mangler;

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
            TypeParameterDataType typeParameterDataType => Build(typeParameterDataType),
            _ => throw new NotImplementedException(),
        };
        _generalCache[dataType] = lowered;

        return lowered;
    }

    public LoweredFunctionDataType BuildInitType(ISemanticInstantiableStructureDeclaration structure, List<IDataType> structureTypeArguments)
    {
        _typeArgumentResolver.PushTypeArguments(structureTypeArguments, structure);
        var parameterTypes = structure
            .Init
            .Parameters
            .Select(x => x.DataType)
            .Select(BuildType)
            .Prepend(BuildType(new StructureDataType(structure.Symbol, structureTypeArguments)))
            .ToList();

        _typeArgumentResolver.PopTypeArguments();
        var voidType = new LoweredPrimitiveDataType(Primitive.Void);

        return new LoweredFunctionDataType(parameterTypes, voidType);
    }

    public LoweredStructDataType BuildVtableType(StructureDataType structureDataType)
    {
        return BuildVtableType(structureDataType, structureDataType);
    }

    public LoweredStructDataType BuildVtableType(StructureDataType implementorDataType, StructureDataType implementedDataType)
    {
        if (_vtableCache.TryGetValue((implementorDataType, implementedDataType), out var existing))
            return existing;

        var implementor = implementorDataType.Symbol.SemanticDeclaration!;
        var implemented = implementedDataType.Symbol.SemanticDeclaration!;
        var functionTypes = implementor
            .Functions
            .Where(x => !x.IsStatic)
            .Select(x => BuildType(new FunctionDataType(x.Symbol)))
            .ToList();
        var name = _mangler.BuildVtableName(implementorDataType, implementedDataType);

        var structValue = new LoweredStructDataType(functionTypes, name);
        _vtableCache[(implementorDataType, implementedDataType)] = structValue;

        return structValue;
    }

    public LoweredStructDataType BuildTypeTableType(StructureDataType structureDataType)
    {
        if (_typeTableCache.TryGetValue(structureDataType, out var existing))
            return existing;

        var vtableType = new LoweredPointerDataType(BuildVtableType(structureDataType));
        var name = _mangler.BuildTypeTableName(structureDataType);
        var structValue = new LoweredStructDataType([vtableType], name);

        _typeTableCache[structureDataType] = structValue;

        return structValue;
    }

    public LoweredStructDataType BuildStructType(StructureSymbol symbol, List<IDataType> typeArguments)
    {
        if (_structCache.TryGetValue(symbol, out var existing))
            return existing;

        var fieldTypes = new List<ILoweredDataType>();
        var loweredType = new LoweredStructDataType(fieldTypes, symbol.Name);
        _structCache[symbol] = loweredType;

        if (symbol.SemanticDeclaration is SemanticClassDeclarationNode classNode)
        {
            fieldTypes.Add(BuildTypeTableType(new StructureDataType(symbol, typeArguments)));

            _typeArgumentResolver.PushTypeArguments(typeArguments, classNode);
            var memberTypes = classNode
                .GetAllMemberFields()
                .Select(x => BuildType(x.DataType));

            fieldTypes.AddRange(memberTypes);
            _typeArgumentResolver.PopTypeArguments();
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

    public LoweredFunctionDataType BuildGetterType(SemanticFieldDeclarationNode field, List<IDataType> structureTypeArguments)
    {
        var fieldType = BuildType(field.DataType);
        List<ILoweredDataType> parameterTypes = [];
        if (!field.IsStatic)
        {
            var selfType = BuildStructType(
                ((ISemanticStructureDeclaration)field.Parent!).Symbol,
                structureTypeArguments
            );
            parameterTypes.Add(new LoweredPointerDataType(selfType));
        }

        return new LoweredFunctionDataType(parameterTypes, fieldType);
    }

    public LoweredFunctionDataType BuildSetterType(SemanticFieldDeclarationNode field, List<IDataType> structureTypeArguments)
    {
        var fieldType = BuildType(field.DataType);
        List<ILoweredDataType> parameterTypes = [fieldType];
        if (!field.IsStatic)
        {
            var selfType = BuildStructType(
                ((ISemanticStructureDeclaration)field.Parent!).Symbol,
                structureTypeArguments
            );
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
            return new LoweredPointerDataType(BuildStructType(dataType.Symbol, dataType.TypeArguments));
        }
        else if (dataType.Symbol.SemanticDeclaration is SemanticProtocolDeclarationNode)
        {
            var internalFatPointerTypes = new List<ILoweredDataType>
            {
                new LoweredPointerDataType(BuildStructType(dataType.Symbol, dataType.TypeArguments)),
                new LoweredPointerDataType(BuildVtableType(dataType)),
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

    private ILoweredDataType Build(TypeParameterDataType dataType)
    {
        return BuildType(_typeArgumentResolver.Resolve(dataType.Symbol));
    }
}
