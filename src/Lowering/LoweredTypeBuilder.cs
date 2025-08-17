using System.Diagnostics;
using Caique.Analysis;
using Caique.Scope;

namespace Caique.Lowering;

class LoweredTypeBuilder(
    GlobalLoweringContext globalLoweringContext,
    TypeArgumentResolver typeArgumentResolver,
    NameMangler mangler
)
{
    private readonly Dictionary<IDataType, ILoweredDataType> _generalCache = [];
    private readonly Dictionary<string, LoweredStructDataType> _vtableCache = [];
    private readonly GlobalLoweringContext _globalLoweringContext = globalLoweringContext;
    private readonly TypeArgumentResolver _typeArgumentResolver = typeArgumentResolver;
    private readonly NameMangler _mangler = mangler;

    public ILoweredDataType BuildType(IDataType dataType)
    {
        if (dataType is not TypeParameterDataType && _generalCache.TryGetValue(dataType, out var existing))
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

        if (dataType is not TypeParameterDataType)
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

    public LoweredStructDataType BuildVtableType(StructureDataType implementorDataType, StructureDataType implementedDataType)
    {
        _typeArgumentResolver.PushTypeArguments(implementorDataType.TypeArguments, implementorDataType.Symbol.SemanticDeclaration!);
        var name = _mangler.BuildVtableName(implementorDataType, implementedDataType);
        if (_vtableCache.TryGetValue(name, out var existing))
            return existing;

        var implementor = implementorDataType.Symbol.SemanticDeclaration!;
        var implemented = implementedDataType.Symbol.SemanticDeclaration!;
        var fields = new List<LoweredStructDataTypeField>();

        // TODO: When default implementions in protocols are a thing, include them here
        var functions = implementor is SemanticClassDeclarationNode { InheritedClass: not null } classDeclaration
            ? classDeclaration.GetVtableMethods()
            : implementor.Functions.Where(x => !x.IsStatic);

        functions = functions.Where(x => !x.IsStatic);

        foreach (var function in functions)
        {
            if (function.Symbol.SyntaxDeclaration.TypeParameters.Count == 0)
            {
                var functionType = BuildType(new FunctionDataType(function.Symbol, implementorDataType, []));
                var functionName = _mangler.BuildUnqualifiedFunctionName(function.Symbol.SemanticDeclaration!, []);
                fields.Add(new LoweredStructDataTypeField(functionName, functionType));
            }
            else
            {
                var placeholder = LoweredStructDataTypeField.CreatePlaceholder();
                fields.Add(placeholder);

                var typeListSpot = new TypeListSpot(fields, placeholder);
                _globalLoweringContext.AddIncompleteFunctionTypeList(function.Symbol, typeListSpot);
            }
        }

        _typeArgumentResolver.PopTypeArguments();
        var dataType = new LoweredStructDataType(fields, name);
        _vtableCache[name] = dataType;

        return dataType;
    }

    public LoweredStructDataType BuildTypeTableType(StructureDataType structureDataType)
    {
        var vtableType = new LoweredPointerDataType(BuildVtableType(structureDataType, structureDataType));
        var name = _mangler.BuildTypeTableName(structureDataType);
        var structValue = new LoweredStructDataType(
            [new LoweredStructDataTypeField("vtable", vtableType)],
            name
        );

        return structValue;
    }

    public LoweredStructDataType BuildStructType(StructureSymbol symbol, List<IDataType> typeArguments)
    {
        var fields = new List<LoweredStructDataTypeField>();
        var loweredType = new LoweredStructDataType(fields, symbol.Name);
        if (symbol.SemanticDeclaration is SemanticClassDeclarationNode classNode)
        {
            var typeTableType = new LoweredPointerDataType(new LoweredPrimitiveDataType(Primitive.Void));
            fields.Add(new LoweredStructDataTypeField("typeTable", typeTableType));

            _typeArgumentResolver.PushTypeArguments(typeArguments, classNode);
            var memberTypes = classNode
                .GetAllMemberFields()
                .Select(x =>
                    new LoweredStructDataTypeField(
                        x.Identifier.Value,
                        BuildType(x.DataType)
                    )
                );

            fields.AddRange(memberTypes);
            _typeArgumentResolver.PopTypeArguments();
        }
        else
        {
            var memberTypes = symbol
                .SemanticDeclaration!
                .Fields
                .Select(x =>
                    new LoweredStructDataTypeField(
                        x.Identifier.Value,
                        BuildType(x.DataType)
                    )
                );
            fields.AddRange(memberTypes);
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

    public LoweredFunctionDataType BuildFunctionType(
        FunctionSymbol symbol,
        List<IDataType> functionTypeArguments,
        List<IDataType>? structureTypeArguments
    )
    {
        if (structureTypeArguments != null)
        {
            var structureDeclaration = SemanticTree.GetEnclosingStructure(symbol.SemanticDeclaration!)!;
            _typeArgumentResolver.PushTypeArguments(structureTypeArguments, structureDeclaration);
        }

        _typeArgumentResolver.PushTypeArguments(functionTypeArguments, symbol.SemanticDeclaration!);

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

        _typeArgumentResolver.PopTypeArguments();
        if (structureTypeArguments != null)
            _typeArgumentResolver.PopTypeArguments();

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
            return new LoweredPointerDataType(new LoweredPrimitiveDataType(Primitive.Void));
        }
        else if (dataType.Symbol.SemanticDeclaration is SemanticProtocolDeclarationNode)
        {
            // var instanceType = new LoweredPointerDataType(BuildStructType(dataType.Symbol, dataType.TypeArguments));
            // var vtableType = new LoweredPointerDataType(BuildVtableType(dataType, dataType));
            var internalFatPointerTypes = new List<LoweredStructDataTypeField>
            {
                new("instance", new LoweredPointerDataType(new LoweredPrimitiveDataType(Primitive.Void))),
                new("vtable", new LoweredPointerDataType(new LoweredPrimitiveDataType(Primitive.Void))),
            };

            return new LoweredStructDataType(internalFatPointerTypes, name: null);
        }

        throw new NotImplementedException();
    }

    private ILoweredDataType Build(FunctionDataType dataType)
    {
        var loweredDataType = BuildFunctionType(
            dataType.Symbol,
            dataType.TypeArguments,
            dataType.InstanceDataType?.TypeArguments
        );

        return new LoweredPointerDataType(loweredDataType);
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
