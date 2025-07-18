using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Caique.Analysis;
using Caique.Backend;
using Caique.Lexing;
using Caique.Scope;

namespace Caique.Lowering;

public class Lowerer
{
    private readonly Dictionary<string, LoweredFunctionDeclarationNode> _functions = [];
    private readonly Dictionary<string, LoweredStructDeclarationNode> _structs = [];
    private readonly Dictionary<string, LoweredGlobalDeclarationNode> _globals = [];
    private readonly Dictionary<ISemanticVariableDeclaration, ILoweredVariableDeclaration> _variableDeclarationCache = [];
    private readonly LoweredTypeBuilder _typeBuilder = new();
    private readonly SemanticTree _tree;
    private readonly NamespaceScope? _stdScope;
    private LoweredBlockNode? _currentBlock;
    private int _nextGlobalStringIndex = 0;

    private Lowerer(SemanticTree tree, NamespaceScope? stdScope)
    {
        _tree = tree;
        _stdScope = stdScope;
    }

    public static LoweredTree Lower(SemanticTree tree, NamespaceScope? stdScope)
    {
        var lowerer = new Lowerer(tree, stdScope);
        lowerer.Next(tree.Root);

        var moduleName = tree.File.Namespace.ToString() + "_" + Path.GetFileNameWithoutExtension(tree.File.FilePath);

        return new LoweredTree(
            moduleName,
            lowerer._functions,
            lowerer._structs,
            lowerer._globals,
            tree.Root.Span.Start.SyntaxTree.File.FilePath
        );
    }

    private LoweredNode? Next(SemanticNode node)
    {
        Debug.Assert(node.Parent != null || node == _tree.Root);

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
            SemanticFieldDeclarationNode fieldDeclarationNode => Visit(fieldDeclarationNode),
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

    private string BuildStaticFieldName(SemanticFieldDeclarationNode field)
    {
        var ffiAttribute = field.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        var parentStructure = _tree.GetEnclosingStructure(field);
        var parentType = new StructureDataType(parentStructure!.Symbol);

        return ffiAttribute == null
            ? $"{parentType}:{field.Identifier.Value}"
            : field.Identifier.Value;
    }

    private string BuildFunctionName(SemanticFunctionDeclarationNode function)
    {
        var ffiAttribute = function.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        if (ffiAttribute != null)
            return function.Identifier.Value;

        var parentStructure = _tree.GetEnclosingStructure(function);
        var parentType = new StructureDataType(parentStructure!.Symbol);

        return $"{parentType}:{function.Identifier.Value}";
    }

    private string BuildStructName(ISemanticStructureDeclaration structure)
    {
        var ffiAttribute = structure.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        if (ffiAttribute != null)
            return structure.Identifier.Value;

        var dataType = new StructureDataType(structure.Symbol);

        return dataType.ToString();
    }

    private string BuildConstructorName(ISemanticStructureDeclaration structure)
    {
        var parentType = new StructureDataType(structure.Symbol);

        return $"{parentType}:init";
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

        var dataType = _typeBuilder.BuildType(new StructureDataType(symbol));
        var valueReference = new LoweredGlobalReferenceNode(name, dataType);

        return BuildNewNode(new StructureDataType(symbol), [valueReference]);
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
        var dataType = _typeBuilder.BuildType(node.DataType);
        var name = BuildFunctionName(node.Symbol.SemanticDeclaration!);

        return new LoweredFunctionReferenceNode(name, dataType);
    }

    private LoweredNode Visit(SemanticFieldReferenceNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        if (node.ObjectInstance == null)
        {
            var name = BuildStaticFieldName(node.Symbol.SemanticDeclaration!);
            var global = new LoweredGlobalReferenceNode(name, dataType);

            return new LoweredLoadNode(global);
        }

        var instance = Next(node.ObjectInstance)!;
        var structure = _tree.GetEnclosingStructure(node.Symbol.SemanticDeclaration!)!;
        var declaration = node.Symbol.SemanticDeclaration!;

        var fieldOffset = 0;
        if (structure is SemanticClassDeclarationNode classNode)
            fieldOffset = CalculateFieldStartIndex(classNode);

        var index = structure.Fields.IndexOf(declaration) + fieldOffset;
        var reference = new LoweredFieldReferenceNode(instance, index, dataType);

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

    private LoweredBinaryNode Visit(SemanticBinaryNode node)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var left = Next(node.Left);
        var right = Next(node.Right);
        Debug.Assert(left != null && right != null);

        return new LoweredBinaryNode(left, node.Operator, right, dataType);
    }

    private LoweredNode Visit(SemanticAssignmentNode node)
    {
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
        var loweredInstanceType = _typeBuilder.BuildType(new StructureDataType(objectInstanceDataType.Symbol));
        var instancePointerDeclaration = new LoweredVariableDeclarationNode(
            "instancePointer",
            objectInstance,
            loweredInstanceType
        );
        Add(instancePointerDeclaration);
        var instancePointerReference = new LoweredVariableReferenceNode(instancePointerDeclaration, loweredInstanceType);

        int functionIndex;
        LoweredPointerDataType vtableType;
        LoweredNode vtableReference;
        if (objectInstanceDataType.Symbol.SemanticDeclaration is SemanticClassDeclarationNode classNode)
        {
            arguments.Insert(0, objectInstance);

            // Get the type table
            var typeTableType = _typeBuilder.BuildTypeTableType(classNode);
            var typeTableReference = new LoweredFieldReferenceNode(
                instancePointerReference,
                0,
                new LoweredPointerDataType(typeTableType)
            );

            // Get the vtable from the type table
            vtableType = new LoweredPointerDataType(_typeBuilder.BuildVtableType(classNode));
            vtableReference = new LoweredFieldReferenceNode(
                typeTableReference,
                0,
                new LoweredPointerDataType(vtableType)
            );

            functionIndex = classNode
                .GetAllMethods()
                .Index()
                .First(x => x.Item.Symbol == functionReference.Symbol)
                .Index;
        }
        else
        {
            // Protocol
            var fatPointerType = (LoweredStructDataType)_typeBuilder.BuildType(objectInstanceDataType);

            // Extract the concrete instance from the fat pointer
            var concreteInstanceReference = new LoweredFieldReferenceNode(
                instancePointerReference,
                (int)FatPointerField.Instance,
                new LoweredPointerDataType(loweredInstanceType)
            );
            var concreteInstancePointer = new LoweredLoadNode(concreteInstanceReference);
            arguments.Insert(0, concreteInstancePointer);

            // Get the vtable
            var vtableIndex = (int)FatPointerField.Vtable;
            vtableType = (LoweredPointerDataType)fatPointerType.FieldTypes[vtableIndex];
            vtableReference = new LoweredFieldReferenceNode(
                instancePointerReference,
                vtableIndex,
                vtableType
            );

            functionIndex = objectInstanceDataType
                .Symbol
                .SemanticDeclaration!
                .Functions
                .IndexOf(functionReference.Symbol.SemanticDeclaration!);
        }

        var vtablePointer = new LoweredLoadNode(vtableReference);

        // Get the function from the vtable
        var functionDeclaration = _typeBuilder.BuildFunctionType(((FunctionDataType)callNode.Left.DataType).Symbol);
        var functionType = ((LoweredStructDataType)vtableType.InnerType).FieldTypes[functionIndex];
        var actualFunctionReference = new LoweredFieldReferenceNode(
            vtablePointer,
            functionIndex,
            functionType
        );
        var actualFunctionPointer = new LoweredLoadNode(actualFunctionReference);

        var name = callNode.DataType.IsVoid()
            ? string.Empty
            : "call";

        return new LoweredCallNode(actualFunctionPointer, arguments, functionDeclaration.ReturnType);
    }

    private LoweredNode Visit(SemanticNewNode node)
    {
        var arguments = node
            .Arguments
            .Select(Next)
            .ToList();

        return BuildNewNode(node.DataType, arguments!);
    }

    private LoweredNode BuildNewNode(IDataType dataType, List<LoweredNode> arguments)
    {
        // TODO: Make some function for this, or maybe cache it somewhere
        var libcUnsafeSymbol = (StructureSymbol)_stdScope!.ResolveSymbol(["libc", "LibcUnsafe"])!;
        var mallocSymbol = (FunctionSymbol)libcUnsafeSymbol.SyntaxDeclaration.Scope.FindSymbol("malloc")!;

        var mallocFunctionName = BuildFunctionName(mallocSymbol.SemanticDeclaration!);
        var mallocType = _typeBuilder.BuildType(new FunctionDataType(mallocSymbol));
        var mallocReference = new LoweredFunctionReferenceNode(mallocFunctionName, mallocType);

        var loweredDataType = _typeBuilder.BuildType(dataType);
        if (loweredDataType is LoweredPointerDataType)
            loweredDataType = loweredDataType.Dereference();

        var sizeValue = new LoweredKeywordValueNode(
            KeywordValueKind.SizeOf,
            [new LoweredTypeNode(loweredDataType)],
            new LoweredPrimitiveDataType(Primitive.USize)
        );

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
            arguments
        );
        Add(call);

        return mallocInstanceReference;
    }

    private LoweredNode BuildConstructorCall(
        LoweredNode instance,
        ISemanticInstantiableStructureDeclaration structure,
        IEnumerable<LoweredNode> arguments
    )
    {
        var name = BuildConstructorName(structure);
        var functionType = _typeBuilder.BuildInitType(structure);
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

    private LoweredKeywordValueNode Visit(SemanticKeywordValueNode node)
    {
        var kind = node.Keyword switch
        {
            { Kind: TokenKind.Self } => KeywordValueKind.Self,
            { Kind: TokenKind.Base } => KeywordValueKind.Base,
            { Value: "size_of" } => KeywordValueKind.SizeOf,
            _ => throw new NotImplementedException(),
        };

        var arguments = node
            .Arguments?
            .Select(Next)
            .ToList() ?? [];
        var dataType = _typeBuilder.BuildType(node.DataType);

        return new LoweredKeywordValueNode(kind, arguments!, dataType);
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

            var instanceIndex = (int)FatPointerField.Instance;
            var instanceType = ((LoweredStructDataType)value.DataType).FieldTypes[instanceIndex];
            var instanceReference = new LoweredFieldReferenceNode(valueReference, instanceIndex, instanceType);

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
            var fatPointerType = _typeBuilder.BuildType((StructureDataType)fromType);
            var fromTypeInstance = new LoweredFieldReferenceNode(value, (int)FatPointerField.Instance, fatPointerType);

            return BuildFatPointer(fromTypeInstance, (StructureDataType)fromType, (StructureDataType)toType);
        }

        if (toType.IsClass() && fromType.IsClass())
        {
            // TODO: Runtime check to make sure it's the correct type
            return value;
        }

        throw new NotImplementedException();
    }

    private LoweredNode BuildFatPointer(LoweredNode instance, StructureDataType implementorDataType, StructureDataType implementedDataType)
    {
        var implementor = implementorDataType.Symbol.SemanticDeclaration!;
        var implemented = implementedDataType.Symbol.SemanticDeclaration!;

        var fatPointerType = (LoweredStructDataType)_typeBuilder.BuildType(implementedDataType);
        var declaration = new LoweredVariableDeclarationNode("fatPointer", null, fatPointerType);
        Add(declaration);

        var reference = new LoweredVariableReferenceNode(
            declaration,
            fatPointerType
        );

        var instanceIndex = (int)FatPointerField.Instance;
        var instanceReference = new LoweredFieldReferenceNode(
            reference,
            instanceIndex,
            fatPointerType.FieldTypes[instanceIndex]
        );
        Add(new LoweredAssignmentNode(instanceReference, instance));

        var vtableIndex = (int)FatPointerField.Vtable;
        var vtableReference = new LoweredFieldReferenceNode(
            reference,
            vtableIndex,
            fatPointerType.FieldTypes[vtableIndex]
        );
        Add(new LoweredAssignmentNode(vtableReference, BuildVtable(implementor, implemented)));

        return new LoweredLoadNode(reference);
    }

    private LoweredBlockNode Visit(SemanticBlockNode node, LoweredVariableDeclarationNode? returnValueDeclaration = null)
    {
        var dataType = _typeBuilder.BuildType(node.DataType);
        var previousBlock = _currentBlock;

        // While lowering things like if expressions, they may provide a return value declaration when calling this
        // function for the branches, to capture the return value of the block. However, free standing blocks can
        // also have return values. In those cases, we will create the variable declaration in here instead.
        if (returnValueDeclaration == null && !node.DataType.IsVoid() && node.Parent is not ISemanticFunctionDeclaration)
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

    private LoweredFunctionDeclarationNode Visit(SemanticFunctionDeclarationNode node)
    {
        var name = BuildFunctionName(node);
        var parameters = node
            .Parameters
            .Select(Visit)
            .ToList();
        var returnType = _typeBuilder.BuildType(node.ReturnType);
        var body = node.Body == null
            ? null
            : (LoweredBlockNode)Next(node.Body)!;

        var last = node.Body?.Expressions.LastOrDefault();
        if (body != null && last is not SemanticReturnNode)
        {
            if (last == null || node.ReturnType is PrimitiveDataType { Kind: Primitive.Void })
                body.Expressions.Add(new LoweredReturnNode(null));
        }

        var dataType = _typeBuilder.BuildFunctionType(node.Symbol);
        var declaration = new LoweredFunctionDeclarationNode(
            name,
            parameters,
            returnType,
            body,
            dataType,
            node.Span
        );
        _functions[name] = declaration;

        return declaration;
    }

    private LoweredStructDeclarationNode Visit(SemanticClassDeclarationNode node)
    {
        var name = BuildStructName(node);
        var fields = node
            .GetAllMemberFields()
            .Select(Visit)
            .ToList();
        var declaration = new LoweredStructDeclarationNode(name, fields, node.Symbol);
        _structs[name] = declaration;

        BuildStaticFields(node.Fields);

        foreach (var function in node.Functions)
            Next(function);

        Visit(node.Init, node);

        return declaration;
    }

    private LoweredFieldDeclarationNode Visit(SemanticFieldDeclarationNode node)
    {
        var identifier = node.Identifier.Value;
        var dataType = _typeBuilder.BuildType(node.DataType);
        var value = node.Value == null
            ? BuildDefaultKeyword(dataType)
            : Next(node.Value)!;
        return new LoweredFieldDeclarationNode(identifier, value, dataType);
    }

    private void BuildStaticFields(List<SemanticFieldDeclarationNode> fields)
    {
        foreach (var staticField in fields.Where(x => x.IsStatic))
        {
            var ffiAttribute = staticField.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
            var staticFieldName = BuildStaticFieldName(staticField);
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

            var globalDeclaration = new LoweredGlobalDeclarationNode(
                staticFieldName,
                staticFieldValue,
                LoweredGlobalScope.Module,
                staticFieldType
            );
            _globals[staticFieldName] = globalDeclaration;
        }
    }

    private LoweredFunctionDeclarationNode Visit(SemanticInitNode node, ISemanticStructureDeclaration parentStructure)
    {
        var block = new LoweredBlockNode([], null, new LoweredPrimitiveDataType(Primitive.Void));
        var previousBlock = _currentBlock;
        _currentBlock = block;

        // Insert default values
        var structureType = _typeBuilder.BuildType(new StructureDataType(parentStructure.Symbol));
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
            var fieldReference = new LoweredFieldReferenceNode(instance, i + fieldOffset, fieldType);
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
                baseCallArguments
            );
        }

        if (parentStructure is SemanticClassDeclarationNode parentClass)
        {
            // Insert type table
            var typeTable = BuildTypeTable(parentClass);
            var typeTableType = _typeBuilder.BuildTypeTableType(parentClass);
            var fieldReference = new LoweredFieldReferenceNode(instance, 0, typeTableType);
            var typeTableAssignment = new LoweredAssignmentNode(fieldReference, typeTable);
            Add(typeTableAssignment);

            // The first field is the type table
            fieldOffset = 1;
        }

        // Parameters need to be evaluated before evaluating the body
        var functionType = _typeBuilder.BuildInitType((ISemanticInstantiableStructureDeclaration)parentStructure);
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

        var name = BuildConstructorName(parentStructure);
        _currentBlock = previousBlock;

        var function = new LoweredFunctionDeclarationNode(
            name,
            parameters,
            functionType.ReturnType,
            block,
            functionType,
            node.Span
        );
        _functions[name] = function;

        return function;
    }

    private LoweredNode BuildVtable(ISemanticStructureDeclaration implementor, ISemanticStructureDeclaration implemented)
    {
        var functionReferences = new List<LoweredNode>();
        foreach (var function in implementor.Functions.Where(x => !x.IsStatic))
        {
            var functionName = BuildFunctionName(function);
            var functionType = _typeBuilder.BuildType(new FunctionDataType(function.Symbol));
            var reference = new LoweredFunctionReferenceNode(functionName, functionType);
            functionReferences.Add(reference);
        }

        var vtableType = _typeBuilder.BuildVtableType(implemented);
        var structValue = new LoweredConstStructNode(functionReferences, vtableType);
        var global = new LoweredGlobalDeclarationNode(
            vtableType.Name!,
            structValue,
            LoweredGlobalScope.Full,
            vtableType
        );
        _globals[vtableType.Name!] = global;

        return new LoweredGlobalReferenceNode(vtableType.Name!, vtableType);
    }

    private LoweredNode BuildTypeTable(SemanticClassDeclarationNode classNode)
    {
        // Prepare the vtable
        var vtableReference = BuildVtable(classNode, classNode);

        // Build the type table
        var typeTableType = _typeBuilder.BuildTypeTableType(classNode);
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

        return structValue;
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
