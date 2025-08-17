using System.Diagnostics;
using System.Runtime.InteropServices;
using Caique.Analysis;
using Caique.Backend;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;

namespace Caique.Lowering;

public class Lowerer
{
    private readonly Dictionary<string, LoweredFunctionDeclarationNode> _functions = [];
    private readonly Dictionary<string, LoweredStructDeclarationNode> _structs = [];
    private readonly Dictionary<string, LoweredGlobalDeclarationNode> _globals = [];
    private readonly Dictionary<ISemanticVariableDeclaration, ILoweredVariableDeclaration> _variableDeclarationCache = [];
    private readonly TypeArgumentResolver _typeArgumentResolver;
    private readonly NameMangler _mangler;
    private readonly LoweredTypeBuilder _typeBuilder;
    private readonly SemanticTree _semanticTree;
    private readonly NamespaceScope? _stdScope;
    private readonly GlobalLoweringContext _globalLoweringContext;
    private LoweredBlockNode? _currentBlock;
    private int _nextGlobalStringIndex = 0;

    private Lowerer(SemanticTree tree, NamespaceScope? stdScope, GlobalLoweringContext globalLoweringContext)
    {
        _typeArgumentResolver = new TypeArgumentResolver();
        _mangler = new NameMangler(_typeArgumentResolver);
        _typeBuilder = new LoweredTypeBuilder(globalLoweringContext, _typeArgumentResolver, _mangler);
        _semanticTree = tree;
        _stdScope = stdScope;
        _globalLoweringContext = globalLoweringContext;
    }

    public static LoweredTree Lower(SemanticTree tree, NamespaceScope? stdScope, GlobalLoweringContext globalLoweringContext)
    {
        var lowerer = new Lowerer(tree, stdScope, globalLoweringContext);
        lowerer.Next(tree.Root);

        var moduleName = tree.File.Namespace.ToString() + "_" + Path.GetFileNameWithoutExtension(tree.File.FilePath);

        return new LoweredTree(
            moduleName,
            tree.Root.Span.Start.SyntaxTree.File,
            lowerer._functions,
            lowerer._structs,
            lowerer._globals
        );
    }

    private LoweredNode? Next(SemanticNode node)
    {
        Debug.Assert(node.Parent != null || node == _semanticTree.Root);

        return node switch
        {
            SemanticStatementNode statementNode => Visit(statementNode),
            SemanticLiteralNode literalNode => Visit(literalNode),
            SemanticVariableReferenceNode variableReferenceNode => Visit(variableReferenceNode),
            SemanticFunctionReferenceNode functionReferenceNode => Visit(functionReferenceNode),
            SemanticFieldReferenceNode fieldReferenceNode => Visit(fieldReferenceNode),
            SemanticEnumReferenceNode enumReferenceNode => Visit(enumReferenceNode),
            SemanticUnaryNode unaryNode => Visit(unaryNode),
            SemanticBinaryNode binaryNode => Visit(binaryNode),
            SemanticAssignmentNode assignmentNode => Visit(assignmentNode),
            SemanticCallNode callNode => Visit(callNode),
            SemanticNewNode newNode => Visit(newNode),
            SemanticReturnNode returnNode => Visit(returnNode),
            SemanticKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            SemanticIfNode ifNode => Visit(ifNode),
            SemanticCastNode castNode => Visit(castNode),
            SemanticBlockNode blockNode => Visit(blockNode),
            SemanticParameterNode parameterNode => Visit(parameterNode),
            SemanticVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SemanticFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SemanticClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            SemanticProtocolDeclarationNode protocolDeclarationNode => Visit(protocolDeclarationNode),
            SemanticModuleDeclarationNode moduleDeclarationNode => Visit(moduleDeclarationNode),
            SemanticEnumDeclarationNode => null,
            _ => throw new NotImplementedException(),
        };
    }

    private void Add(LoweredNode node)
    {
        _currentBlock!.Expressions.Add(node);
    }

    private LoweredKeywordValueNode BuildDefaultKeyword(ILoweredDataType dataType)
    {
        return new LoweredKeywordValueNode(KeywordValueKind.Default, [], dataType);
    }

    private LoweredNode BuildString(string value)
    {
        var symbol = _stdScope!.ResolveStructure(["prelude", "String"])!;
        var name = $"str.{_nextGlobalStringIndex}";
        _nextGlobalStringIndex++;

        var stringLiteralType = new LoweredPointerDataType(new LoweredPrimitiveDataType(Primitive.Int8));
        var stringLiteral = new LoweredLiteralNode(value, TokenKind.StringLiteral, stringLiteralType);
        var valueDeclaration = new LoweredGlobalDeclarationNode(
            name,
            stringLiteral,
            LoweredGlobalScope.Module,
            stringLiteralType
        );
        _globals[name] = valueDeclaration;

        var semanticDataType = new StructureDataType(symbol, []);
        var dataType = _typeBuilder.BuildType(semanticDataType);
        var valueReference = new LoweredGlobalReferenceNode(name, dataType);
        var lengthType = new LoweredPrimitiveDataType(Primitive.USize);
        var lengthLiteral = new LoweredLiteralNode(value.Length.ToString(), TokenKind.NumberLiteral, lengthType);

        return BuildNewNode(semanticDataType, [valueReference, lengthLiteral]);
    }

    private bool FieldHasGetter(SemanticFieldDeclarationNode field)
    {
        if (field.Getter != null)
            return true;

        if (field.Value == null)
            return false;

        if (field.Value is SemanticLiteralNode && field.DataType is PrimitiveDataType)
            return false;

        return field.IsStatic;
    }

    private bool FieldHasSetter(SemanticFieldDeclarationNode field)
    {
        if (field.Setter != null)
            return true;

        return field.IsMutable && FieldHasGetter(field);
    }

    private LoweredNode? Visit(SemanticStatementNode node)
    {
        Add(new LoweredStatementStartNode(node.Span));
        var value = Next(node.Value);

        if (value == null)
            return null;

        Add(value);

        return value;
    }

    private LoweredNode Visit(SemanticLiteralNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        if (node.DataType.IsString())
            return BuildString(node.Value.Value);

        return new LoweredLiteralNode(node.Value.Value, node.Value.Kind, dataType);
    }

    private LoweredLoadNode Visit(SemanticVariableReferenceNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var declaration = _variableDeclarationCache[node.Symbol.SemanticDeclaration!];
        var reference = new LoweredVariableReferenceNode(declaration, dataType);

        return new LoweredLoadNode(reference);
    }

    private LoweredFunctionReferenceNode Visit(SemanticFunctionReferenceNode node)
    {
        var structureTypeArguments = new List<IDataType>();
        ISemanticStructureDeclaration? structureDeclaration = null;
        if (node.ObjectInstance?.DataType is StructureDataType structureInstance)
        {
            structureTypeArguments = structureInstance.TypeArguments;
            structureDeclaration = structureInstance.Symbol.SemanticDeclaration!;
        }

        var functionDataType = (FunctionDataType)node.DataType;
        var name = _mangler.BuildFunctionName(
            node.Symbol.SemanticDeclaration!,
            functionDataType.TypeArguments,
            structureTypeArguments
        );

        if (node.Symbol.SyntaxDeclaration.TypeParameters.Count > 0)
        {
            GenerateFunctionOnDemand(
                name,
                node.Symbol,
                functionDataType,
                structureDeclaration,
                structureTypeArguments
            );
        }

        var dataType = _typeBuilder.BuildType(node.DataType);

        return new LoweredFunctionReferenceNode(name, dataType);
    }

    private void GenerateFunctionOnDemand(
        string name,
        FunctionSymbol symbol,
        FunctionDataType functionDataType,
        ISemanticStructureDeclaration? structureDeclaration,
        List<IDataType> structureTypeArguments
    )
    {
        var functionFileScope = SyntaxTree.GetFileScope(symbol.SyntaxDeclaration)!;
        var unqualifiedName = _mangler.BuildUnqualifiedFunctionName(symbol.SemanticDeclaration!, functionDataType.TypeArguments);
        _globalLoweringContext.AddOnDemandGenericFunction(
            symbol,
            name,
            unqualifiedName,
            functionFileScope,
            () =>
            {
                if (structureDeclaration != null)
                    _typeArgumentResolver.PushTypeArguments(structureTypeArguments, structureDeclaration);

                var declaration = functionDataType.Symbol.SemanticDeclaration!;
                _typeArgumentResolver.PushTypeArguments(functionDataType.TypeArguments, declaration);

                var loweredDeclaration = BuildFunctionDeclaration(
                    declaration,
                    functionDataType.TypeArguments,
                    structureTypeArguments
                );

                _typeArgumentResolver.PopTypeArguments();
                if (structureDeclaration != null)
                    _typeArgumentResolver.PopTypeArguments();

                return loweredDeclaration;
            }
        );
    }

    private void GenerateFunctionOnDemandForAllSpecialisationsOfStructure(
        FunctionSymbol implementationSymbol,
        FunctionDataType virtualFunctionDataType,
        StructureSymbol structureSymbol,
        List<IDataType> structureTypeArguments
    )
    {
        // Pass on the _typeArgumentResolver, which is cloned and saved for the later generation.
        // Also save a closure that will be used to generate the function.
        var functionFileScope = SyntaxTree.GetFileScope(implementationSymbol.SyntaxDeclaration)!;
        _globalLoweringContext.AddOnDemandGenericFunctionForAllSpecialisationsOfStructure(
            implementationSymbol,
            functionFileScope,
            virtualFunctionDataType,
            structureSymbol,
            structureTypeArguments,
            _typeArgumentResolver,
            (typeArgumentResolver, instanceDeclaration) =>
            {
                var pushCount = _typeArgumentResolver.PushAllFromOtherResolver(typeArgumentResolver);

                var semanticDeclaration = implementationSymbol.SemanticDeclaration!;
                var typeArguments = virtualFunctionDataType.TypeArguments;
                var declaration = BuildFunctionDeclarationWithPregeneratedInstance(
                    semanticDeclaration,
                    typeArguments,
                    instanceDeclaration
                );
                var unqualifiedName = _mangler.BuildUnqualifiedFunctionName(semanticDeclaration, typeArguments);

                for (var i = 0; i < pushCount; i++)
                    _typeArgumentResolver.PopTypeArguments();

                return (declaration, unqualifiedName);
            }
        );
    }

    private LoweredNode Visit(SemanticFieldReferenceNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var declaration = node.Symbol.SemanticDeclaration!;
        var structureDataType = node.ObjectInstance?.DataType as StructureDataType;
        var structureTypeArguments = structureDataType?.TypeArguments ?? [];
        if (FieldHasGetter(declaration))
        {
            var identifier = _mangler.BuildGetterName(declaration, structureTypeArguments);
            var getterDataType = _typeBuilder.BuildGetterType(declaration, structureTypeArguments);
            var getterReference = new LoweredFunctionReferenceNode(
                identifier,
                new LoweredPointerDataType(getterDataType)
            );

            List<LoweredNode> arguments = [];
            if (node.ObjectInstance != null)
                arguments.Add(Next(node.ObjectInstance)!);

            return new LoweredCallNode(getterReference, arguments, dataType);
        }

        if (node.ObjectInstance == null)
        {
            var name = _mangler.BuildStaticFieldName(node.Symbol.SemanticDeclaration!);
            var global = new LoweredGlobalReferenceNode(name, dataType);

            return new LoweredLoadNode(global);
        }

        var instance = Next(node.ObjectInstance)!;
        var structure = SemanticTree.GetEnclosingStructure(node.Symbol.SemanticDeclaration!)!;

        var fieldOffset = 0;
        if (structure is SemanticClassDeclarationNode classNode)
            fieldOffset = CalculateFieldStartIndex(classNode);

        var index = structure.Fields.IndexOf(declaration) + fieldOffset;
        var instanceDataType = _typeBuilder.BuildStructType(structureDataType!.Symbol, structureTypeArguments);
        var reference = new LoweredFieldReferenceNode(instanceDataType, instance, index, dataType);

        return new LoweredLoadNode(reference);
    }

    private static int CalculateFieldStartIndex(SemanticClassDeclarationNode classDeclarationNode)
    {
        // The first field is the type table. Therefore, the minimum offset is 1
        return classDeclarationNode.InheritedClass?.SemanticDeclaration is SemanticClassDeclarationNode inheritedClass
            ? CalculateFieldStartIndex(inheritedClass) + inheritedClass.Fields.Count
            : 1;
    }

    private LoweredNode Visit(SemanticEnumReferenceNode node)
    {
        var member = node
            .Symbol
            .SemanticDeclaration!
            .Members
            .First(x => x.Identifier.Value == node.Identifier.Value);

        return Next(member.Value)!;
    }

    private LoweredUnaryNode Visit(SemanticUnaryNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var value = Next(node.Value);
        Debug.Assert(value != null);

        return new LoweredUnaryNode(node.Operator, value, dataType);
    }

    private LoweredNode Visit(SemanticBinaryNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var left = Next(node.Left);
        var right = Next(node.Right);
        Debug.Assert(left != null && right != null);

        if (node.Left.DataType is StructureDataType structureDataType)
        {
            if (node.Operator is TokenKind.EqualsEquals)
                return BuildEquatableCall(structureDataType, left, right);

            if (node.Operator is TokenKind.NotEquals)
            {
                var isEqual = BuildEquatableCall(structureDataType, left, right);

                return new LoweredUnaryNode(TokenKind.Exclamation, isEqual, isEqual.DataType);
            }
        }

        return new LoweredBinaryNode(left, node.Operator, right, dataType);
    }

    private LoweredCallNode BuildEquatableCall(StructureDataType structureDataType, LoweredNode instance, LoweredNode other)
    {
        var boolType = new LoweredPrimitiveDataType(Primitive.Bool);
        // TODO: Optimise
        var isEqualFunctionDeclaration = structureDataType
            .Symbol
            .SemanticDeclaration!
            .Functions
            .First(x => x.Identifier.Value == "IsEqual");
        var isEqualFunctionType = _typeBuilder.BuildFunctionType(
            isEqualFunctionDeclaration.Symbol,
            [],
            structureDataType.TypeArguments
        );
        var isEqualFunctionName = _mangler.BuildFunctionName(
            isEqualFunctionDeclaration,
            [],
            structureDataType.TypeArguments
        );
        var isEqualFunctionReference = new LoweredFunctionReferenceNode(
            isEqualFunctionName,
            new LoweredPointerDataType(isEqualFunctionType)
        );

        return new LoweredCallNode(isEqualFunctionReference, [instance, other], boolType);
    }

    private LoweredNode Visit(SemanticAssignmentNode node)
    {
        if (node.Left is SemanticFieldReferenceNode fieldReference && FieldHasSetter(fieldReference.Symbol.SemanticDeclaration!))
        {
            var structureTypeArguments = fieldReference.ObjectInstance?.DataType is StructureDataType instanceDataType
                ? instanceDataType.TypeArguments
                : [];
            var declaration = fieldReference.Symbol.SemanticDeclaration!;
            var identifier = _mangler.BuildSetterName(declaration, structureTypeArguments);
            var setterDataType = _typeBuilder.BuildSetterType(declaration, structureTypeArguments);
            var getterReference = new LoweredFunctionReferenceNode(
                identifier,
                new LoweredPointerDataType(setterDataType)
            );

            var setterValue = Next(node.Right)!;
            List<LoweredNode> arguments = [setterValue];
            if (fieldReference.ObjectInstance != null)
                arguments.Insert(0, Next(fieldReference.ObjectInstance)!);

            return new LoweredCallNode(getterReference, arguments, setterDataType.ReturnType);
        }

        var assignee = Next(node.Left) as LoweredLoadNode;
        var value = Next(node.Right);
        Debug.Assert(assignee != null && value != null);

        return new LoweredAssignmentNode(assignee.Value, value);
    }

    private LoweredCallNode Visit(SemanticCallNode node)
    {
        var arguments = node
            .Arguments
            .Select(Next)
            .ToList();

        if (node.Left is SemanticFunctionReferenceNode { ObjectInstance: not null } functionReference)
        {
            var objectInstance = Next(functionReference.ObjectInstance)!;
            if (functionReference.Symbol.IsVirtual)
            {
                var objectInstanceDataType = (StructureDataType)functionReference.ObjectInstance.DataType;

                return BuildVirtualCall(node, objectInstanceDataType, objectInstance, functionReference, arguments!);
            }

            arguments.Insert(0, objectInstance);
        }

        var dataType = _typeBuilder.BuildType(node.DataType);
        var callee = Next(node.Left);
        Debug.Assert(callee != null);
        Debug.Assert(arguments.All(x => x != null));

        return new LoweredCallNode(callee, arguments!, dataType);
    }

    private LoweredCallNode BuildVirtualCall(
        SemanticCallNode callNode,
        StructureDataType objectInstanceDataType,
        LoweredNode objectInstance,
        SemanticFunctionReferenceNode functionReference,
        List<LoweredNode> arguments
    )
    {
        var loweredInstanceType = _typeBuilder.BuildType(objectInstanceDataType);
        var instancePointerDeclaration = new LoweredVariableDeclarationNode(
            "instancePointer",
            objectInstance,
            loweredInstanceType
        );
        Add(instancePointerDeclaration);
        var instancePointerReference = new LoweredVariableReferenceNode(instancePointerDeclaration, loweredInstanceType);

        string functionName;
        LoweredNode vtableReference;
        var functionDataType = (FunctionDataType)callNode.Left.DataType;
        var vtableType = _typeBuilder.BuildVtableType(objectInstanceDataType, objectInstanceDataType);
        if (objectInstanceDataType.Symbol.SemanticDeclaration is SemanticClassDeclarationNode classNode)
        {
            arguments.Insert(0, objectInstance);

            // Get the type table
            var typeTableType = _typeBuilder.BuildTypeTableType(objectInstanceDataType);
            var loweredInstanceStructureDataType = _typeBuilder.BuildStructType(objectInstanceDataType.Symbol, objectInstanceDataType.TypeArguments);
            var typeTableReference = new LoweredFieldReferenceNode(
                loweredInstanceStructureDataType,
                instancePointerReference,
                0,
                new LoweredPointerDataType(typeTableType)
            );

            // Get the vtable from the type table
            vtableReference = new LoweredFieldReferenceNode(
                typeTableType,
                new LoweredLoadNode(typeTableReference),
                0,
                new LoweredPointerDataType(vtableType)
            );

            var declaration = classNode
                .GetAllMethods()
                .First(x => x.Symbol == functionReference.Symbol);
            functionName = _mangler.BuildFunctionName(
                declaration,
                functionDataType.TypeArguments,
                functionDataType.InstanceDataType?.TypeArguments ?? []
            );
        }
        else
        {
            // Protocol
            var fatPointerType = (LoweredStructDataType)_typeBuilder.BuildType(objectInstanceDataType);

            // Extract the concrete instance from the fat pointer
            var concreteInstanceReference = new LoweredFieldReferenceNode(
                fatPointerType,
                instancePointerReference,
                (int)FatPointerField.Instance,
                new LoweredPointerDataType(loweredInstanceType)
            );
            var concreteInstancePointer = new LoweredLoadNode(concreteInstanceReference);
            arguments.Insert(0, concreteInstancePointer);

            // Get the vtable
            var vtableIndex = (int)FatPointerField.Vtable;
            vtableReference = new LoweredFieldReferenceNode(
                fatPointerType,
                instancePointerReference,
                vtableIndex,
                new LoweredPointerDataType(vtableType)
            );

            var declaration = objectInstanceDataType
                .Symbol
                .SemanticDeclaration!
                .Functions
                .First(x => x == functionReference.Symbol.SemanticDeclaration);
            functionName = _mangler.BuildFunctionName(
                declaration,
                functionDataType.TypeArguments,
                functionDataType.InstanceDataType?.TypeArguments ?? []
            );
        }

        var vtablePointer = new LoweredLoadNode(vtableReference);

        var name = callNode.DataType.IsVoid()
            ? string.Empty
            : "call";

        if (functionDataType.Symbol.SyntaxDeclaration.TypeParameters.Count > 0)
        {
            var implementors = objectInstanceDataType
                .Symbol
                .GetAllNestedImplementors()
                .Append(objectInstanceDataType.Symbol);
            foreach (var implementationSymbol in implementors)
            {
                var symbol = implementationSymbol
                    .SemanticDeclaration!
                    .Functions
                    .FirstOrDefault(x => x.Identifier.Value == functionDataType.Symbol.SemanticDeclaration!.Identifier.Value)?
                    .Symbol;
                if (symbol == null)
                    continue;

                GenerateFunctionOnDemandForAllSpecialisationsOfStructure(
                    symbol,
                    functionDataType,
                    implementationSymbol,
                    objectInstanceDataType.TypeArguments
                );
            }
        }

        // Get the function from the vtable
        var functionDeclaration = _typeBuilder.BuildFunctionType(
            functionDataType.Symbol,
            functionDataType.TypeArguments,
            objectInstanceDataType.TypeArguments
        );

        // We can't get the field by index since vtable fields are created on-demand
        // for generic functions.
        var unqualifiedName = _mangler.BuildUnqualifiedFunctionName(
            functionDataType.Symbol.SemanticDeclaration!,
            functionDataType.TypeArguments
        );
        var actualFunctionReference = new LoweredFieldReferenceByNameNode(
            vtableType,
            vtablePointer,
            unqualifiedName,
            new LoweredPointerDataType(functionDeclaration)
        );
        var actualFunctionPointer = new LoweredLoadNode(actualFunctionReference);

        return new LoweredCallNode(actualFunctionPointer, arguments, functionDeclaration.ReturnType);
    }

    private LoweredNode Visit(SemanticNewNode node)
    {
        var arguments = node
            .Arguments
            .Select(Next)
            .ToList();
        var structureDataType = (StructureDataType)node.DataType;
        var loweredNode = BuildNewNode(structureDataType, arguments!);

        if (structureDataType.Symbol.SyntaxDeclaration.TypeParameters.Count > 0)
            GenerateStructOnDemand(structureDataType);

        return loweredNode;
    }

    private void GenerateStructOnDemand(StructureDataType structureDataType)
    {
        var structureFileScope = SyntaxTree.GetFileScope((SyntaxNode)structureDataType.Symbol.SyntaxDeclaration)!;
        var name = _mangler.BuildStructName(structureDataType.Symbol.SemanticDeclaration!, structureDataType.TypeArguments);
        _globalLoweringContext.AddOnDemandGenericStructure(
            structureDataType.Symbol,
            name,
            structureFileScope,
            () =>
            {
                var declaration = structureDataType.Symbol.SemanticDeclaration!;
                _typeArgumentResolver.PushTypeArguments(structureDataType.TypeArguments, declaration);

                var loweredDeclaration = BuildStructureDeclaration(declaration, structureDataType.TypeArguments);
                _typeArgumentResolver.PopTypeArguments();

                return loweredDeclaration;
            }
        );
    }

    private LoweredNode BuildNewNode(StructureDataType dataType, List<LoweredNode> arguments)
    {
        // TODO: Make some function for this, or maybe cache it somewhere
        var libcUnsafeSymbol = (StructureSymbol)_stdScope!.ResolveSymbol(["libc", "LibcUnsafe"])!;
        var mallocSymbol = (FunctionSymbol)libcUnsafeSymbol.SyntaxDeclaration.Scope.FindSymbol("malloc")!;

        var mallocFunctionName = _mangler.BuildFunctionName(mallocSymbol.SemanticDeclaration!, [], dataType.TypeArguments);
        var mallocType = _typeBuilder.BuildType(new FunctionDataType(mallocSymbol, null, []));
        var mallocReference = new LoweredFunctionReferenceNode(mallocFunctionName, mallocType);

        var loweredDataType = _typeBuilder.BuildStructType(dataType.Symbol, dataType.TypeArguments);
        var sizeValue = new LoweredSizeOfNode(loweredDataType);
        var instance = new LoweredCallNode(mallocReference, [sizeValue], loweredDataType);
        var declaration = ((StructureDataType)dataType).Symbol.SemanticDeclaration!;
        var instanceCast = new LoweredCastNode(instance, new LoweredPointerDataType(loweredDataType));
        var instanceDeclaration = new LoweredVariableDeclarationNode(
            "mallocInstance",
            instanceCast,
            instanceCast.DataType
        );
        Add(instanceDeclaration);

        var mallocInstanceReference = new LoweredLoadNode(
            new LoweredVariableReferenceNode(
                instanceDeclaration,
                instanceDeclaration.DataType
            )
        );
        var call = BuildConstructorCall(
            mallocInstanceReference,
            (ISemanticInstantiableStructureDeclaration)declaration,
            dataType.TypeArguments,
            arguments
        );
        Add(call);

        return mallocInstanceReference;
    }

    private LoweredNode BuildConstructorCall(
        LoweredNode instance,
        ISemanticInstantiableStructureDeclaration structure,
        List<IDataType> structureTypeArguments,
        IEnumerable<LoweredNode> arguments
    )
    {
        var functionType = _typeBuilder.BuildInitType(structure, structureTypeArguments);
        var name = _mangler.BuildConstructorName(structure, structureTypeArguments);
        var functionReference = new LoweredFunctionReferenceNode(
            name,
            new LoweredPointerDataType(functionType)
        );

        var builtArguments = arguments
            .Prepend(instance)
            .ToList();

        return new LoweredCallNode(functionReference, builtArguments, functionType.ReturnType);
    }

    private LoweredReturnNode Visit(SemanticReturnNode node)
    {
        var value = node.Value == null
            ? null
            : Next(node.Value);

        return new LoweredReturnNode(value);
    }

    private LoweredNode Visit(SemanticKeywordValueNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        if (node.Keyword.Value == "value")
        {
            var field = SemanticTree.GetEnclosingField(node);
            Debug.Assert(field != null);

            return BuildSetterValueKeyword(field);
        }

        if (node.Keyword.Value == "size_of")
        {
            var argumentDataType = ((SemanticTypeNode)node.Arguments![0]).DataType;
            var loweredArgument = _typeBuilder.BuildType(argumentDataType);

            return new LoweredSizeOfNode(loweredArgument);
        }

        var kind = node.Keyword switch
        {
            { Kind: TokenKind.Self } => KeywordValueKind.Self,
            { Kind: TokenKind.Base } => KeywordValueKind.Base,
            _ => throw new NotImplementedException(),
        };

        var arguments = node
            .Arguments?
            .Select(Next)
            .ToList() ?? [];

        return new LoweredKeywordValueNode(kind, arguments!, dataType);
    }

    private LoweredNode BuildSetterValueKeyword(SemanticFieldDeclarationNode field)
    {
        var dataType = _typeBuilder.BuildType(field.DataType);
        var setterIdentifier = _mangler.BuildSetterName(field, _typeArgumentResolver.GetCurrentStructureTypeArguments());
        var setter = _functions[setterIdentifier];
        var valueDeclaration = setter.Parameters.Last();
        var valueReference = new LoweredVariableReferenceNode(valueDeclaration, dataType);

        return new LoweredLoadNode(valueReference);
    }

    private LoweredIfNode Visit(SemanticIfNode node)
    {
        LoweredVariableDeclarationNode? returnValueDeclaration = null;
        if (!node.ThenBranch.DataType.IsVoid())
        {
            var dataType = _typeBuilder.BuildType(node.ThenBranch.DataType);
            returnValueDeclaration = new LoweredVariableDeclarationNode("blockReturnValue", null, dataType);
            Add(returnValueDeclaration);
        }

        var condition = Next(node.Condition)!;
        var thenBranch = Visit(node.ThenBranch, returnValueDeclaration)!;
        var elseBranch = node.ElseBranch == null
            ? null
            : Visit(node.ElseBranch, returnValueDeclaration);

        return new LoweredIfNode(
            condition,
            thenBranch,
            elseBranch,
            thenBranch.DataType
        );
    }

    private LoweredNode Visit(SemanticCastNode node)
    {
        var fromType = node.Value.DataType;
        var toType = node.DataType;

        var value = Next(node.Value)!;
        if (toType is EnumDataType toEnum)
            toType = toEnum.Symbol.SemanticDeclaration!.MemberDataType;

        if (fromType is EnumDataType fromEnum)
            fromType = fromEnum.Symbol.SemanticDeclaration!.MemberDataType;

        var loweredToType = _typeBuilder.BuildType(toType);
        if (toType is PrimitiveDataType)
            return new LoweredCastNode(value, loweredToType);

        if (toType.IsClass() && fromType.IsProtocol())
        {
            // Extract the instance pointer from the fat pointer
            var valueDeclaration = new LoweredVariableDeclarationNode("instance", value, value.DataType);
            Add(valueDeclaration);
            var valueReference = new LoweredVariableReferenceNode(valueDeclaration, value.DataType);

            var fatPointerType = (LoweredStructDataType)_typeBuilder.BuildType((StructureDataType)fromType);
            var instanceIndex = (int)FatPointerField.Instance;
            var instanceType = ((LoweredStructDataType)value.DataType).Fields[instanceIndex].DataType;
            var instanceReference = new LoweredFieldReferenceNode(fatPointerType, valueReference, instanceIndex, instanceType);

            return new LoweredLoadNode(instanceReference);
        }

        if (toType.IsProtocol() && fromType.IsClass())
        {
            // TODO: Runtime check to make sure it's the correct type
            return BuildFatPointer(value, (StructureDataType)fromType, (StructureDataType)toType);
        }

        if (toType.IsProtocol() && fromType.IsProtocol())
        {
            // TODO: Runtime check to make sure it's the correct type

            // Extract the instance pointer from the existing fat pointer
            var fatPointerType = (LoweredStructDataType)_typeBuilder.BuildType((StructureDataType)fromType);
            var fromTypeInstance = new LoweredFieldReferenceNode(fatPointerType, value, (int)FatPointerField.Instance, fatPointerType);

            return BuildFatPointer(fromTypeInstance, (StructureDataType)fromType, (StructureDataType)toType);
        }

        if (toType.IsClass() && fromType.IsClass())
        {
            // TODO: Runtime check to make sure it's the correct type
            return value;
        }

        throw new NotImplementedException();
    }

    private LoweredNode BuildFatPointer(
        LoweredNode instance,
        StructureDataType implementorDataType,
        StructureDataType implementedDataType
    )
    {
        var fatPointerType = (LoweredStructDataType)_typeBuilder.BuildType(implementedDataType);
        var declaration = new LoweredVariableDeclarationNode("fatPointer", null, fatPointerType);
        Add(declaration);

        var reference = new LoweredVariableReferenceNode(
            declaration,
            fatPointerType
        );

        var instanceIndex = (int)FatPointerField.Instance;
        var instanceReference = new LoweredFieldReferenceNode(
            fatPointerType,
            reference,
            instanceIndex,
            fatPointerType.Fields[instanceIndex].DataType
        );
        Add(new LoweredAssignmentNode(instanceReference, instance));

        var vtableIndex = (int)FatPointerField.Vtable;
        var vtable = GetOrBuildVtable(implementorDataType, implementedDataType);
        var vtableReference = new LoweredFieldReferenceNode(
            fatPointerType,
            reference,
            vtableIndex,
            fatPointerType.Fields[vtableIndex].DataType
        );
        Add(new LoweredAssignmentNode(vtableReference, vtable));

        return new LoweredLoadNode(reference);
    }

    private LoweredBlockNode Visit(SemanticBlockNode node, LoweredVariableDeclarationNode? returnValueDeclaration = null)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var previousBlock = _currentBlock;

        // While lowering things like if expressions, they may provide a return value declaration when calling this
        // function for the branches, to capture the return value of the block. However, free standing blocks can
        // also have return values. In those cases, we will create the variable declaration in here instead.
        if (returnValueDeclaration == null &&
            !node.DataType.IsVoid() &&
            node.Parent is not (ISemanticFunctionDeclaration or SemanticFieldDeclarationNode))
        {
            returnValueDeclaration = new LoweredVariableDeclarationNode("returnValue", null, dataType);
            Add(returnValueDeclaration);
        }

        var block = new LoweredBlockNode([], returnValueDeclaration, dataType);

        _currentBlock = block;
        foreach (var child in node.Expressions)
            Next(child);

        var returnValueReference = returnValueDeclaration == null
            ? null
            : new LoweredVariableReferenceNode(returnValueDeclaration, dataType);
        if (returnValueReference != null)
            block.Expressions[^1] = new LoweredAssignmentNode(returnValueReference, block.Expressions[^1]);

        _currentBlock = previousBlock;

        return block;
    }

    private LoweredParameterNode Visit(SemanticParameterNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var parameter = new LoweredParameterNode(node.Identifier.Value, dataType);
        _variableDeclarationCache[node] = parameter;

        return parameter;
    }

    private LoweredVariableDeclarationNode Visit(SemanticVariableDeclarationNode node)
    {
        var value = Next(node.Value)!;
        var dataType = _typeBuilder.BuildType(node.DataType);
        var declaration = new LoweredVariableDeclarationNode(node.Identifier.Value, value, dataType);
        _variableDeclarationCache[node] = declaration;

        return declaration;
    }

    private LoweredFunctionDeclarationNode? Visit(
        SemanticFunctionDeclarationNode node,
        List<IDataType>? structureTypeArguments = null
    )
    {
        // Function with type parameters are generated on-demand when the types are encountered
        if (node.Symbol.SyntaxDeclaration.TypeParameters.Count == 0)
        {
            var loweredDeclaration = BuildFunctionDeclaration(node, [], structureTypeArguments);
            _functions[loweredDeclaration.Identifier] = loweredDeclaration;
        }

        return null;
    }

    private LoweredFunctionDeclarationNode BuildFunctionDeclaration(
        SemanticFunctionDeclarationNode node,
        List<IDataType>? functionTypeArguments,
        List<IDataType>? structureTypeArguments
    )
    {
        var name = _mangler.BuildFunctionName(
            node,
            functionTypeArguments ?? [],
            structureTypeArguments ?? []
        );
        var parameters = node
            .Parameters
            .Select(Visit)
            .ToList();

        var dataType = _typeBuilder.BuildFunctionType(node.Symbol, functionTypeArguments ?? [], structureTypeArguments);
        if (!node.IsStatic)
        {
            var instanceType = dataType.ParameterTypes[0];
            parameters.Insert(0, new LoweredParameterNode("self", instanceType));
        }

        return BuildFunctionDeclarationWithParameters(name, node, parameters, dataType);
    }

    private LoweredFunctionDeclarationNode? BuildFunctionDeclarationWithPregeneratedInstance(
        SemanticFunctionDeclarationNode node,
        List<IDataType>? functionTypeArguments,
        LoweredStructDeclarationNode loweredInstanceDeclaration
    )
    {
        var name = _mangler.BuildFunctionNameWithStructName(
            node,
            functionTypeArguments ?? [],
            loweredInstanceDeclaration.Identifier
        );
        if (_functions.ContainsKey(name))
            return null;

        var selfType = new LoweredPointerDataType(new LoweredPrimitiveDataType(Primitive.Void));
        var parameters = node
            .Parameters
            .Select(Visit)
            .Prepend(new LoweredParameterNode("self", selfType))
            .ToList();
        var parameterTypes = parameters
            .Select(x => x.DataType)
            .ToList();
        var returnType = _typeBuilder.BuildType(node.ReturnType);
        var dataType = new LoweredFunctionDataType(parameterTypes, returnType);

        return BuildFunctionDeclarationWithParameters(name, node, parameters, dataType);
    }

    private LoweredFunctionDeclarationNode BuildFunctionDeclarationWithParameters(
        string name,
        SemanticFunctionDeclarationNode node,
        List<LoweredParameterNode> parameters,
        ILoweredDataType loweredDataType
    )
    {
        var declaration = new LoweredFunctionDeclarationNode(
            name,
            parameters,
            body: null,
            loweredDataType,
            node.Span
        );
        _functions[name] = declaration;

        var body = node.Body == null
            ? null
            : (LoweredBlockNode)Next(node.Body)!;

        var last = node.Body?.Expressions.LastOrDefault();
        if (body != null && last is not SemanticReturnNode)
        {
            if (last == null || node.ReturnType is PrimitiveDataType { Kind: Primitive.Void })
                body.Expressions.Add(new LoweredReturnNode(null));
        }

        declaration.Body = body;

        return declaration;
    }

    private LoweredNode? Visit(SemanticClassDeclarationNode node)
    {
        // Classes with type parameters are generated on-demand when the types are encountered
        if (node.Symbol.SyntaxDeclaration.TypeParameters.Count == 0)
        {
            var loweredDeclaration = BuildClassDeclaration(node, []);
            _structs[loweredDeclaration.Identifier] = loweredDeclaration;
            _globalLoweringContext.AddPregeneratedStructure(node.Symbol, loweredDeclaration);
        }

        return null;
    }

    private LoweredStructDeclarationNode BuildStructureDeclaration(ISemanticStructureDeclaration node, List<IDataType> typeArguments)
    {
        return node switch
        {
            SemanticClassDeclarationNode classNode => BuildClassDeclaration(classNode, typeArguments),
            _ => throw new InvalidOperationException(),
        };
    }

    private LoweredStructDeclarationNode BuildClassDeclaration(
        SemanticClassDeclarationNode node,
        List<IDataType> typeArguments
    )
    {
        if (node.InheritedClass != null && node.InheritedClass.SyntaxDeclaration.TypeParameters.Count > 0)
        {
            var baseDataType = new StructureDataType(node.InheritedClass.SyntaxDeclaration.Symbol!, typeArguments);
            GenerateStructOnDemand(baseDataType);
        }

        foreach (var protocol in node.ImplementedProtocols)
        {
            if (protocol.SyntaxDeclaration.TypeParameters.Count > 0)
            {
                var protocolDataType = new StructureDataType(protocol.SyntaxDeclaration.Symbol!, typeArguments);
                GenerateStructOnDemand(protocolDataType);
            }
        }

        var name = _mangler.BuildStructName(node, typeArguments);
        var fields = node
            .GetAllMemberFields()
            .Select(x => Visit(x, typeArguments))
            .Where(x => x != null)
            .Cast<LoweredFieldDeclarationNode>()
            .ToList();
        var declaration = new LoweredStructDeclarationNode(name, fields, node.Symbol);

        BuildStaticFields(node.Fields);

        foreach (var function in node.Functions)
            Visit(function, typeArguments);

        Visit(node.Init, node, typeArguments);

        return declaration;
    }

    private LoweredNode? Visit(SemanticFieldDeclarationNode node, List<IDataType> structureTypeArguments)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        if (FieldHasGetter(node))
        {
            var getterIdentifier = _mangler.BuildGetterName(node, structureTypeArguments);
            var getterDataType = _typeBuilder.BuildGetterType(node, structureTypeArguments);
            var parameters = getterDataType
                .ParameterTypes
                .Select((x, i) => new LoweredParameterNode(i.ToString(), x))
                .ToList();
            var getterFunction = new LoweredFunctionDeclarationNode(
                getterIdentifier,
                parameters,
                body: null,
                getterDataType,
                node.Span
            );

            _functions[getterIdentifier] = getterFunction;
            getterFunction.Body = node.Getter == null
            ? BuildLazyFieldGetter(node, structureTypeArguments)
                : Visit(node.Getter);
        }

        if (FieldHasSetter(node))
        {
            Debug.Assert(FieldHasGetter(node));

            var setterIdentifier = _mangler.BuildSetterName(node, structureTypeArguments);
            var setterDataType = _typeBuilder.BuildSetterType(node, structureTypeArguments);
            var parameters = setterDataType
                .ParameterTypes
                .Select((x, i) => new LoweredParameterNode(i.ToString(), x))
                .ToList();
            var setterFunction = new LoweredFunctionDeclarationNode(
                setterIdentifier,
                parameters,
                body: null,
                setterDataType,
                node.Span
            );

            _functions[setterIdentifier] = setterFunction;
            setterFunction.Body = node.Setter == null
                ? BuildLazyFieldSetter(node, structureTypeArguments)
                : Visit(node.Setter);

            if (setterFunction.Body.Expressions.LastOrDefault() is not LoweredReturnNode)
                setterFunction.Body.Expressions.Add(new LoweredReturnNode(null));
        }

        if (node.Getter != null)
            return null;

        var identifier = node.Identifier.Value;
        var value = node.Value == null
            ? BuildDefaultKeyword(dataType)
            : Next(node.Value)!;
        return new LoweredFieldDeclarationNode(identifier, value, dataType);
    }

    private LoweredBlockNode BuildLazyFieldGetter(SemanticFieldDeclarationNode field, List<IDataType> structureTypeArguments)
    {
        // TODO: Add lock (or atomic operation?)

        // static let `field.get.initialised` bool = false;
        var initialisedDeclarationName = _mangler.BuildGetterName(field, structureTypeArguments) + ".initialised";
        var boolType = new LoweredPrimitiveDataType(Primitive.Bool);
        var falseLiteral = new LoweredLiteralNode(
            string.Empty,
            TokenKind.False,
            boolType
        );
        var initialisedDeclaration = new LoweredGlobalDeclarationNode(
            initialisedDeclarationName,
            falseLiteral,
            LoweredGlobalScope.Module,
            boolType
        );
        _globals[initialisedDeclarationName] = initialisedDeclaration;

        // static let `field.get.value` T = default;
        var dataType = _typeBuilder.BuildType(field.DataType);
        var valueDeclarationName = _mangler.BuildGetterName(field, structureTypeArguments) + ".value";
        var valueDeclaration = new LoweredGlobalDeclarationNode(
            valueDeclarationName,
            BuildDefaultKeyword(dataType),
            LoweredGlobalScope.Module,
            dataType
        );
        _globals[valueDeclarationName] = valueDeclaration;

        // Build getter body
        var voidType = new LoweredPrimitiveDataType(Primitive.Void);
        var block = new LoweredBlockNode([], returnValueDeclaration: null, voidType);

        // if `field.get.initialised`
        var condition = new LoweredLoadNode(
            new LoweredGlobalReferenceNode(initialisedDeclarationName, boolType)
        );

        // -> return `field.get.value`
        var thenBranch = new LoweredBlockNode([], returnValueDeclaration: null, voidType);
        var valueReference = new LoweredLoadNode(
            new LoweredGlobalReferenceNode(valueDeclarationName, dataType)
        );
        thenBranch.Expressions.Add(new LoweredReturnNode(valueReference));

        // else { `field.get.value` = value, `field.get.initialised` = true }
        var elseBranch = new LoweredBlockNode([], returnValueDeclaration: null, voidType);
        var valueAssignment = new LoweredAssignmentNode(
            new LoweredGlobalReferenceNode(valueDeclarationName, dataType),
            Next(field.Value!)!
        );
        elseBranch.Expressions.Add(valueAssignment);

        var trueLiteral = new LoweredLiteralNode(
            string.Empty,
            TokenKind.True,
            boolType
        );
        var initialisedAssignment = new LoweredAssignmentNode(
            new LoweredGlobalReferenceNode(initialisedDeclarationName, boolType),
            trueLiteral
        );
        elseBranch.Expressions.Add(initialisedAssignment);
        elseBranch.Expressions.Add(new LoweredReturnNode(valueReference));

        var ifNode = new LoweredIfNode(
            condition,
            thenBranch,
            elseBranch,
            voidType
        );
        block.Expressions.Add(ifNode);

        return block;
    }

    private LoweredBlockNode BuildLazyFieldSetter(SemanticFieldDeclarationNode field, List<IDataType> structureTypeArguments)
    {
        var voidType = new LoweredPrimitiveDataType(Primitive.Void);
        var block = new LoweredBlockNode([], returnValueDeclaration: null, voidType);

        var dataType = _typeBuilder.BuildType(field.DataType);
        var valueDeclarationName = _mangler.BuildGetterName(field, structureTypeArguments) + ".value";
        var valueReference = new LoweredGlobalReferenceNode(valueDeclarationName, dataType);
        var value = BuildSetterValueKeyword(field);
        var assignment = new LoweredAssignmentNode(valueReference, value);
        block.Expressions.Add(assignment);

        return block;
    }

    private void BuildStaticFields(List<SemanticFieldDeclarationNode> fields)
    {
        foreach (var staticField in fields.Where(x => x.IsStatic))
        {
            if (FieldHasGetter(staticField))
            {
                Visit(staticField, structureTypeArguments: []);

                continue;
            }

            var ffiAttribute = staticField.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
            var staticFieldName = _mangler.BuildStaticFieldName(staticField);
            var staticFieldType = _typeBuilder.BuildType(staticField.DataType);

            LoweredNode? staticFieldValue;
            if (ffiAttribute != null)
            {
                staticFieldValue = null;
            }
            else if (staticField.Value == null)
            {
                staticFieldValue = BuildDefaultKeyword(staticFieldType);
            }
            else
            {
                staticFieldValue = Next(staticField.Value)!;
            }

            var scope = staticField.IsPublic
                ? LoweredGlobalScope.Full
                : LoweredGlobalScope.Module;
            var globalDeclaration = new LoweredGlobalDeclarationNode(
                staticFieldName,
                staticFieldValue,
                scope,
                staticFieldType
            );
            _globals[staticFieldName] = globalDeclaration;
        }
    }

    private LoweredFunctionDeclarationNode Visit(
        SemanticInitNode node,
        ISemanticStructureDeclaration parentStructure,
        List<IDataType> structureTypeArguments
    )
    {
        var block = new LoweredBlockNode([], null, new LoweredPrimitiveDataType(Primitive.Void));
        var previousBlock = _currentBlock;
        _currentBlock = block;

        // Insert default values
        var structureTypeParameters = parentStructure
            .Symbol
            .SyntaxDeclaration
            .TypeParameters
            .Select(x => new TypeParameterDataType(x.Symbol))
            .Cast<IDataType>()
            .ToList();
        var structureType = _typeBuilder.BuildType(new StructureDataType(parentStructure.Symbol, structureTypeParameters));
        var instance = new LoweredKeywordValueNode(KeywordValueKind.Self, [], structureType);

        int fieldOffset = parentStructure is SemanticClassDeclarationNode
            ? 1
            : 0;
        var fields = parentStructure is SemanticClassDeclarationNode classNode
            ? classNode.GetAllMemberFields()
            : parentStructure.Fields;
        foreach (var (i, field) in fields.Index())
        {
            var fieldType = _typeBuilder.BuildType(field.DataType);
            var loweredStructureDataType = _typeBuilder.BuildStructType(parentStructure.Symbol, structureTypeArguments);
            var fieldReference = new LoweredFieldReferenceNode(loweredStructureDataType, instance, i + fieldOffset, fieldType);
            var value = field.Value == null
                ? BuildDefaultKeyword(fieldType)
                : Next(field.Value)!;
            var assignment = new LoweredAssignmentNode(fieldReference, value);
            Add(assignment);
        }

        // Call base constructor (if any)
        if (parentStructure is SemanticClassDeclarationNode { InheritedClass: not null } classNode2)
        {
            IEnumerable<LoweredNode> baseCallArguments = [];
            if (node.BaseCall != null)
            {
                baseCallArguments = node
                    .BaseCall
                    .Arguments!
                    .Select(Next)!;
            }

            BuildConstructorCall(
                instance,
                (SemanticClassDeclarationNode)classNode2.InheritedClass!.SemanticDeclaration!,
                structureTypeArguments,
                baseCallArguments
            );
        }

        if (parentStructure is SemanticClassDeclarationNode parentClass)
        {
            // Insert type table
            var structureDataType = new StructureDataType(parentClass.Symbol, structureTypeArguments);
            var typeTable = BuildTypeTable(structureDataType);
            var typeTableType = _typeBuilder.BuildTypeTableType(structureDataType);
            var loweredStructureDataType = _typeBuilder.BuildStructType(parentClass.Symbol, structureTypeArguments);
            var fieldReference = new LoweredFieldReferenceNode(loweredStructureDataType, instance, 0, typeTableType);
            var typeTableAssignment = new LoweredAssignmentNode(fieldReference, typeTable);
            Add(typeTableAssignment);

            // The first field is the type table
            fieldOffset = 1;
        }

        // Parameters need to be evaluated before evaluating the body
        var functionType = _typeBuilder.BuildInitType(
            (ISemanticInstantiableStructureDeclaration)parentStructure,
            structureTypeArguments
        );
        var instanceParameter = new LoweredParameterNode("instance", functionType.ParameterTypes.First());
        var parameters = node
            .Parameters
            .Select(Visit)
            .Prepend(instanceParameter)
            .ToList();

        foreach (var expression in node.Body.Expressions)
        {
            var value = Next(expression)!;
            Add(value);
        }

        if (node.Body.Expressions.LastOrDefault() is not SemanticReturnNode)
        {
            var returnNode = new LoweredReturnNode(null);
            Add(returnNode);
        }

        var name = _mangler.BuildConstructorName(parentStructure, structureTypeArguments);
        _currentBlock = previousBlock;

        var function = new LoweredFunctionDeclarationNode(
            name,
            parameters,
            block,
            functionType,
            node.Span
        );
        _functions[name] = function;

        return function;
    }

    private LoweredNode GetOrBuildVtable(StructureDataType implementorDataType, StructureDataType implementedDataType)
    {
        var vtableType = _typeBuilder.BuildVtableType(implementedDataType, implementorDataType);
        LoweredConstStructNode? structValue = null;

        if (!_globalLoweringContext.VtableHasBeenRegistered(vtableType.Name!))
        {
            var implementor = implementorDataType.Symbol.SemanticDeclaration!;
            var implemented = implementedDataType.Symbol.SemanticDeclaration!;

            // TODO: When default implementions in protocols are a thing, include them here
            var functions = implementor is SemanticClassDeclarationNode { InheritedClass: not null } classDeclaration
                ? classDeclaration.GetVtableMethods()
                : implementor.Functions.Where(x => !x.IsStatic);

            var functionReferences = new List<LoweredNode>();
            foreach (var function in functions)
            {
                if (function.Symbol.SyntaxDeclaration.TypeParameters.Count == 0)
                {
                    var functionName = _mangler.BuildFunctionName(function, [], implementorDataType.TypeArguments);
                    var functionType = _typeBuilder.BuildType(new FunctionDataType(function.Symbol, implementorDataType, []));
                    var reference = new LoweredFunctionReferenceNode(functionName, functionType);
                    functionReferences.Add(reference);
                }
                else
                {
                    var placeholder = new LoweredOnDemandReferencePlaceholderNode();
                    functionReferences.Add(placeholder);

                    var referenceListSpot = new ReferenceListSpot(functionReferences, placeholder);
                    _globalLoweringContext.AddIncompleteFunctionReferenceList(function.Symbol, referenceListSpot);
                }
            }

            vtableType = _typeBuilder.BuildVtableType(implementedDataType, implementorDataType);
            structValue = new LoweredConstStructNode(
                functionReferences,
                vtableType
            );
        }

        var global = new LoweredGlobalDeclarationNode(
            vtableType.Name!,
            structValue,
            LoweredGlobalScope.Full,
            vtableType
        );
        _globals[vtableType.Name!] = global;
        _globalLoweringContext.RegisterVtable(vtableType.Name!);

        return new LoweredGlobalReferenceNode(vtableType.Name!, vtableType);
    }

    private LoweredNode BuildTypeTable(StructureDataType classDataType)
    {
        // Prepare the vtable
        var vtableReference = GetOrBuildVtable(classDataType, classDataType);

        // Build the type table
        var typeTableType = _typeBuilder.BuildTypeTableType(classDataType);
        var fields = new List<LoweredNode>()
        {
            vtableReference,
        };

        var structValue = new LoweredConstStructNode(fields, typeTableType);
        var global = new LoweredGlobalDeclarationNode(
            typeTableType.Name!,
            structValue,
            LoweredGlobalScope.Full,
            structValue.DataType
        );
        _globals[typeTableType.Name!] = global;

        return new LoweredLoadNode(
            new LoweredGlobalReferenceNode(typeTableType.Name!, typeTableType)
        );
    }

    private LoweredNode? Visit(SemanticProtocolDeclarationNode node)
    {
        foreach (var function in node.Functions)
            Next(function);

        return null;
    }

    private LoweredNode? Visit(SemanticModuleDeclarationNode node)
    {
        BuildStaticFields(node.Fields);

        foreach (var function in node.Functions)
            Next(function);

        return null;
    }
}
