using System.Diagnostics;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;

namespace Caique.Analysis;

public class Analyser
{
    private readonly TextSpan _blankSpan;
    private readonly SyntaxTree _syntaxTree;
    private readonly DiagnosticReporter _diagnostics;
    private readonly NamespaceScope _preludeScope;

    private Analyser(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        _syntaxTree = syntaxTree;
        _diagnostics = compilationContext.DiagnosticReporter;
        _preludeScope = compilationContext.PreludeScope;
        _blankSpan = new TextSpan(
            new TextPosition(-1, -1, -1, syntaxTree),
            new TextPosition(-1, -1, -1, syntaxTree)
        );
    }

    public static SemanticTree Analyse(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        var analyser = new Analyser(syntaxTree, compilationContext);
        Debug.Assert(syntaxTree.Root != null);
        var root = analyser.Next(syntaxTree.Root);
        root.Traverse((node, parent) => node.Parent = parent);

        return new SemanticTree(root);
    }

    private SemanticNode Next(SyntaxNode node)
    {
        Debug.Assert(node.Parent != null || node == _syntaxTree.Root);

        return node switch
        {
            SyntaxStatementNode statementNode => Visit(statementNode),
            SyntaxGroupNode groupNode => Visit(groupNode),
            SyntaxLiteralNode literalNode => Visit(literalNode),
            SyntaxIdentifierNode identifierNode => Visit(identifierNode),
            SyntaxUnaryNode unaryNode => Visit(unaryNode),
            SyntaxBinaryNode binaryNode => Visit(binaryNode),
            SyntaxAssignmentNode assignmentNode => Visit(assignmentNode),
            SyntaxMemberAccessNode memberAccessNode => Visit(memberAccessNode),
            SyntaxCallNode callNode => Visit(callNode),
            SyntaxNewNode newNode => Visit(newNode),
            SyntaxReturnNode returnNode => Visit(returnNode),
            SyntaxKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            SyntaxDotKeywordNode dotKeywordNode => Visit(dotKeywordNode),
            SyntaxBlockNode blockNode => Visit(blockNode),
            SyntaxAttributeNode attributeNode => Visit(attributeNode),
            SyntaxParameterNode parameterNode => Visit(parameterNode),
            SyntaxTypeNode typeNode => Visit(typeNode),
            SyntaxVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SyntaxFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SyntaxClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            SyntaxProtocolDeclarationNode protocolDeclarationNode => Visit(protocolDeclarationNode),
            SyntaxModuleDeclarationNode moduleDeclarationNode => Visit(moduleDeclarationNode),
            SyntaxFieldDeclarationNode fieldDeclarationNode => Visit(fieldDeclarationNode),
            SyntaxInitNode initNode => Visit(initNode),
            SyntaxInitParameterNode initParameterNode => Visit(initParameterNode),
            _ => throw new NotImplementedException(),
        };
    }

    private AnalyserRecoveryException Recover()
    {
        return new AnalyserRecoveryException();
    }

    private SemanticKeywordValueNode BuildSelfNode(StructureSymbol structureSymbol)
    {
        var token = new Token(TokenKind.Self, string.Empty, _blankSpan);

        return new SemanticKeywordValueNode(token, [], _blankSpan, new StructureDataType(structureSymbol));
    }

    private SemanticStatementNode Visit(SyntaxStatementNode node)
    {
        var value = Next(node.Expression);

        return new SemanticStatementNode(value);
    }

    private SemanticNode Visit(SyntaxGroupNode node)
    {
        return Next(node.Value);
    }

    private SemanticKeywordValueNode Visit(SyntaxKeywordValueNode node)
    {
        var arguments = node
            .Arguments?
            .Select(Next)
            .ToList();

        if (node.Keyword.Value == "size_of")
            return NextSizeOfKeyword(node, arguments);

        if (node.Keyword.Kind == TokenKind.Base)
            return NextBaseKeyword(node, arguments);

        throw new UnreachableException();
    }

    private SemanticKeywordValueNode NextSizeOfKeyword(SyntaxKeywordValueNode node, List<SemanticNode>? arguments)
    {
        if (arguments?.Count != 1)
        {
            arguments = [];
            _diagnostics.ReportWrongNumberOfArguments(1, arguments.Count, node.Span);
        }

        return new SemanticKeywordValueNode(node.Keyword, arguments, node.Span, new PrimitiveDataType(Primitive.Int64));
    }

    private SemanticKeywordValueNode NextBaseKeyword(SyntaxKeywordValueNode node, List<SemanticNode>? arguments)
    {
        var structure = _syntaxTree.GetEnclosingStructure(node);
        if (arguments == null)
        {
            Debug.Assert(structure?.Symbol != null);

            return new SemanticKeywordValueNode(node.Keyword, null, node.Span, new StructureDataType(structure.Symbol));
        }

        if (node.Parent?.Parent?.Parent is not SyntaxInitNode)
            _diagnostics.ReportMisplacedBaseCall(node.Span);

        var baseClassNode = (structure as SyntaxClassDeclarationNode)?
            .SubTypes
            .FirstOrDefault(x => x.ResolvedSymbol?.SyntaxDeclaration is SyntaxClassDeclarationNode)?
            .ResolvedSymbol?
            .SyntaxDeclaration as SyntaxClassDeclarationNode;

        if (baseClassNode == null)
        {
            _diagnostics.ReportMisplacedBaseCall(node.Span);
            throw Recover();
        }

        Debug.Assert(structure?.Symbol != null);

        var parameterTypes = baseClassNode
            .Init?
            .Parameters
            .Select(Next)
            .Select(x => x.DataType)
            .ToList();
        var typeCheckedArguments = TypeCheckArguments(parameterTypes ?? [], arguments, node.Span);

        return new SemanticKeywordValueNode(
            node.Keyword,
            typeCheckedArguments,
            node.Span,
            new StructureDataType(structure.Symbol)
        );
    }

    private SemanticNode Visit(SyntaxDotKeywordNode node)
    {
        var arguments = node
            .Arguments?
            .Select(Next)
            .ToList();

        if (node.Keyword.Kind == TokenKind.As)
            return NextAsKeyword(node, arguments);

        throw new UnreachableException();
    }

    private SemanticCastNode NextAsKeyword(SyntaxDotKeywordNode node, List<SemanticNode>? arguments)
    {
        if (arguments?.Count != 1)
        {
            _diagnostics.ReportWrongNumberOfArguments(1, arguments?.Count ?? 0, node.Span);
            throw Recover();
        }

        var value = Next(node.Left);
        var targetDataType = arguments.Single().DataType;

        if (value.DataType.GetType() != targetDataType.GetType())
            _diagnostics.ReportInvalidCast(value.DataType, targetDataType, value.Span);

        if (value.DataType.IsVoid() || targetDataType.IsVoid())
            _diagnostics.ReportInvalidCast(value.DataType, targetDataType, value.Span);

        if ((value.DataType.IsBoolean() != targetDataType.IsBoolean()))
            _diagnostics.ReportInvalidCast(value.DataType, targetDataType, value.Span);

        return new SemanticCastNode(value, node.Span, targetDataType);
    }

    private SemanticLiteralNode Visit(SyntaxLiteralNode node)
    {
        if (node.Value.Kind == TokenKind.NumberLiteral)
        {
            var primitiveKind = node.Value.Value.Contains('.')
                ? Primitive.Float32
                : Primitive.Int32;
            var dataType = new PrimitiveDataType(primitiveKind);

            return new SemanticLiteralNode(node.Value, dataType);
        }

        if (node.Value.Kind is TokenKind.True or TokenKind.False)
        {
            var dataType = new PrimitiveDataType(Primitive.Bool);

            return new SemanticLiteralNode(node.Value, dataType);
        }

        if (node.Value.Kind == TokenKind.StringLiteral)
        {
            var symbol = _preludeScope.ResolveStructure(["String"])!;
            var dataType = new StructureDataType(symbol);

            return new SemanticLiteralNode(node.Value, dataType);
        }

        throw new NotImplementedException();
    }

    private SemanticNode Visit(SyntaxIdentifierNode node)
    {
        if (node.IdentifierList.Count > 1)
        {
            var namespaceNames = node.IdentifierList
                .Take(node.IdentifierList.Count - 1)
                .Select(x => x.Value)
                .ToList();
            var structureSymbol = _syntaxTree.File.ResolveStructure(namespaceNames);
            if (structureSymbol == null)
            {
                _diagnostics.ReportNotFound(node.IdentifierList);
                throw Recover();
            }

            var identifierSymbol = structureSymbol.SyntaxDeclaration.Scope.FindSymbol(node.IdentifierList.Last().Value);
            if (identifierSymbol == null)
            {
                _diagnostics.ReportNotFound(node.IdentifierList);
                throw Recover();
            }

            if (identifierSymbol is FunctionSymbol functionSymbol)
            {
                if (!functionSymbol.SyntaxDeclaration.IsStatic)
                {
                    _diagnostics.ReportNonStaticSymbolReferencedAsStatic(node.IdentifierList.Last());
                }

                var dataType = new FunctionDataType(functionSymbol);

                return new SemanticFunctionReferenceNode(
                    node.IdentifierList.Last(),
                    functionSymbol,
                    objectInstance: null,
                    dataType
                );
            }

            if (identifierSymbol is FieldSymbol fieldSymbol)
            {
                if (!fieldSymbol.SyntaxDeclaration.IsStatic)
                {
                    _diagnostics.ReportNonStaticSymbolReferencedAsStatic(node.IdentifierList.Last());
                }

                // TODO: All fields should be private, so this isn't allowed
                var dataType = Next(fieldSymbol.SyntaxDeclaration.Type).DataType;

                return new SemanticFieldReferenceNode(
                    node.IdentifierList.Last(),
                    fieldSymbol,
                    objectInstance: null,
                    dataType
                );
            }

            throw new NotImplementedException();
        }

        var identifier = node.IdentifierList.Single();
        var variableSymbol = _syntaxTree.GetLocalScope(node)?.FindSymbol(identifier.Value);
        if (variableSymbol != null)
            return new SemanticVariableReferenceNode(identifier, variableSymbol);

        var structure = _syntaxTree.GetEnclosingStructure(node);
        Debug.Assert(structure?.Symbol != null);

        var symbolFromStructure = structure?.Scope.FindSymbol(identifier.Value);
        if (symbolFromStructure is FunctionSymbol functionSymbol2)
        {
            var dataType = new FunctionDataType(functionSymbol2);
            var instance = functionSymbol2.SyntaxDeclaration.IsStatic
                ? null
                : BuildSelfNode(structure!.Symbol);

            return new SemanticFunctionReferenceNode(identifier, functionSymbol2, instance, dataType);
        }

        if (symbolFromStructure is FieldSymbol fieldSymbol2)
        {
            var dataType = Next(fieldSymbol2.SyntaxDeclaration.Type).DataType;
            var instance = fieldSymbol2.SyntaxDeclaration.IsStatic
                ? null
                : BuildSelfNode(structure!.Symbol);

            Debug.Assert(structure?.Symbol != null);

            // TODO: Verify that the parent function isn't static since you can't reference
            // non-static fields from static functions
            return new SemanticFieldReferenceNode(identifier, fieldSymbol2, instance, dataType);
        }

        _diagnostics.ReportNotFound(identifier);
        throw Recover();
    }

    private SemanticNode Visit(SyntaxUnaryNode node)
    {
        var value = Next(node.Value);
        if (node.Operator == TokenKind.Exclamation && value.DataType is not PrimitiveDataType { Kind: Primitive.Bool })
        {
            _diagnostics.ReportIncompatibleType(Primitive.Bool, value.DataType, node.Span);
            throw Recover();
        }

        if (node.Operator == TokenKind.Minus && !value.DataType.IsNumber())
        {
            _diagnostics.ReportIncompatibleType("number", value.DataType, node.Span);
            throw Recover();
        }

        return new SemanticUnaryNode(node.Operator, value, value.DataType, node.Span);
    }

    private SemanticNode Visit(SyntaxBinaryNode node)
    {
        var left = Next(node.Left);
        var right = Next(node.Right);
        if (node.Operator is TokenKind.Plus or TokenKind.Minus or TokenKind.Star or TokenKind.Slash)
        {
            if (!left.DataType.IsNumber())
            {
                _diagnostics.ReportIncompatibleType("number", left.DataType, left.Span);
                throw Recover();
            }

            right = TypeCheck(right, left.DataType);
        }
        else if (node.Operator is TokenKind.AmpersandAmpersand or TokenKind.PipePipe)
        {
            if (left.DataType is not PrimitiveDataType { Kind: Primitive.Bool })
            {
                _diagnostics.ReportIncompatibleType(Primitive.Bool, left.DataType, left.Span);
                throw Recover();
            }

            if (right.DataType is not PrimitiveDataType { Kind: Primitive.Bool })
            {
                _diagnostics.ReportIncompatibleType(Primitive.Bool, right.DataType, right.Span);
                throw Recover();
            }

        }
        else if (node.Operator is TokenKind.EqualsEquals or TokenKind.NotEquals)
        {
            right = TypeCheck(right, left.DataType);
        }

        return new SemanticBinaryNode(left, node.Operator, right, left.DataType);
    }

    private SemanticAssignmentNode Visit(SyntaxAssignmentNode node)
    {
        var left = Next(node.Left);
        var right = Next(node.Right);

        if (left is not (SemanticVariableReferenceNode or SemanticFieldReferenceNode))
        {
            _diagnostics.ReportExpectedVariableReferenceInAssignment(node.Span);
        }

        right = TypeCheck(right, left.DataType);

        return new SemanticAssignmentNode(left, right, node.Span);
    }

    private SemanticNode Visit(SyntaxMemberAccessNode node)
    {
        var left = Next(node.Left);
        if (left.DataType is StructureDataType structureDataType)
        {
            var symbol = structureDataType.Symbol.SyntaxDeclaration.ResolveSymbol(node.Identifier.Value);
            if (symbol == null)
            {
                _diagnostics.ReportMemberNotFound(node.Identifier, left.DataType);
                throw Recover();
            }

            if (symbol is FunctionSymbol functionSymbol)
            {
                var dataType = new FunctionDataType(functionSymbol);
                if (node.Parent is not SyntaxCallNode)
                {
                    _diagnostics.ReportNonStaticFunctionReferenceMustBeCalled(node.Identifier);
                }

                if (functionSymbol.SyntaxDeclaration.IsStatic)
                {
                    _diagnostics.ReportStaticSymbolReferencedAsNonStatic(functionSymbol.SyntaxDeclaration.Identifier);
                }

                return new SemanticFunctionReferenceNode(
                    functionSymbol.SyntaxDeclaration.Identifier,
                    functionSymbol,
                    objectInstance: left,
                    dataType
                );
            }
            else if (symbol is FieldSymbol fieldSymbol)
            {
                var dataType = Next(fieldSymbol.SyntaxDeclaration.Type).DataType;

                if (fieldSymbol.SyntaxDeclaration.IsStatic)
                {
                    _diagnostics.ReportStaticSymbolReferencedAsNonStatic(fieldSymbol.SyntaxDeclaration.Identifier);
                }

                return new SemanticFieldReferenceNode(
                    fieldSymbol.SyntaxDeclaration.Identifier,
                    fieldSymbol,
                    objectInstance: left,
                    dataType
                );
            }
            else
            {
                throw new UnreachableException();
            }
        }

        _diagnostics.ReportMemberNotFound(node.Identifier, left.DataType);
        throw Recover();
    }

    private SemanticCallNode Visit(SyntaxCallNode node)
    {
        var left = Next(node.Left);
        var arguments = node.Arguments.Select(Next).ToList();

        if (left.DataType is not FunctionDataType functionDataType)
        {
            _diagnostics.ReportIncompatibleType("function reference", left.DataType, left.Span);
            throw Recover();
        }

        var parameterTypes = functionDataType
            .Symbol
            .SyntaxDeclaration
            .Parameters
            .Select(x => Next(x.Type).DataType)
            .ToList();
        var typeCheckedArguments = TypeCheckArguments(parameterTypes, arguments, node.Span);

        var declarationReturnType = functionDataType.Symbol.SyntaxDeclaration.ReturnType;
        var dataType = declarationReturnType == null
            ? PrimitiveDataType.Void
            : Next(declarationReturnType).DataType;

        return new SemanticCallNode(left, typeCheckedArguments, dataType, node.Span);
    }

    private SemanticNewNode Visit(SyntaxNewNode node)
    {
        var dataType = Next(node.Type).DataType;
        if (dataType is not StructureDataType structureDataType)
        {
            _diagnostics.ReportIncompatibleType("name of structure", dataType, node.Span);
            throw Recover();
        }

        SyntaxInitNode? initNode;
        var instantiable = structureDataType.Symbol.SyntaxDeclaration as ISyntaxInstantiableStructureDeclaration;
        if (instantiable != null)
        {
            initNode = instantiable.Init;
        }
        else
        {
            initNode = null;
            _diagnostics.ReportIncompatibleType("name of instantiable structure", dataType, node.Span);
        }

        var arguments = node
            .Arguments
            .Select(Next)
            .ToList();
        var parameterTypes = initNode?
            .Parameters
            .Select(Next)
            .Select(x => x.DataType)
            .ToList()
            ?? [];

        // Will be null if it isn't an instantiable structure (which is an error, so don't try to type check)
        if (instantiable != null)
            arguments = TypeCheckArguments(parameterTypes, arguments, node.Span);

        return new SemanticNewNode(arguments, dataType, node.Span);
    }

    private List<SemanticNode> TypeCheckArguments(List<IDataType> parameterTypes, List<SemanticNode> arguments, TextSpan span)
    {
        if (parameterTypes.Count != arguments.Count)
            _diagnostics.ReportWrongNumberOfArguments(parameterTypes.Count, arguments.Count, span);

        return arguments
            .Zip(parameterTypes)
            .Select(x => TypeCheck(x.First, x.Second))
            .ToList();
    }

    private SemanticNode TypeCheck(SemanticNode value, IDataType targetDataType)
    {
        var equivalence = value.DataType.IsEquivalent(targetDataType);
        if (equivalence == TypeEquivalence.Incompatible)
        {
            _diagnostics.ReportIncompatibleType(targetDataType, value.DataType, value.Span);

            return value;
        }
        else if (equivalence == TypeEquivalence.ImplicitCast)
        {
            return new SemanticCastNode(value, value.Span, targetDataType);
        }

        return value;
    }

    private SemanticReturnNode Visit(SyntaxReturnNode node)
    {
        var value = node.Value == null
            ? null
            : Next(node.Value);

        var enclosingFunction = _syntaxTree.GetEnclosingFunction(node);
        if (enclosingFunction == null)
        {
            _diagnostics.ReportReturnOutsideFunction(node.Span);
            throw Recover();
        }

        value = TypeCheckReturnType(enclosingFunction, value, node.Span);

        return new SemanticReturnNode(value, node.Span);
    }

    private SemanticNode? TypeCheckReturnType(ISyntaxFunctionDeclaration enclosingFunction, SemanticNode? value, TextSpan span)
    {
        var functionReturnType = enclosingFunction.ReturnType == null
            ? null
            : Next(enclosingFunction.ReturnType).DataType;

        if (value == null)
        {
            if (functionReturnType != null)
                _diagnostics.ReportIncompatibleType(Primitive.Void, functionReturnType, span);

            return null;
        }

        return TypeCheck(value!, functionReturnType ?? PrimitiveDataType.Void);
    }

    private SemanticNode Visit(SyntaxBlockNode node)
    {
        var expressions = new List<SemanticNode>();
        foreach (var child in node.Expressions.SkipLast(1))
        {
            if (ShouldSkipBlockChild(child))
                continue;

            try
            {
                expressions.Add(Next(child));
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        IDataType dataType = PrimitiveDataType.Void;
        var last = node.Expressions.LastOrDefault();
        if (last == null || ShouldSkipBlockChild(last))
            return new SemanticBlockNode(expressions, dataType, node.Span);

        SemanticNode? analysedLast;
        try
        {
            analysedLast = Next(last);
        }
        catch (AnalyserRecoveryException)
        {
            return new SemanticBlockNode(expressions, dataType, node.Span);
        }

        if (last is SyntaxStatementNode { IsReturnValue: true })
        {
            if (node.Parent is ISyntaxFunctionDeclaration enclosingFunction)
            {
                var typeCheckedValue = TypeCheckReturnType(enclosingFunction, analysedLast, node.Span);
                analysedLast = new SemanticReturnNode(typeCheckedValue, node.Span);
            }
        }

        expressions.Add(analysedLast);

        return new SemanticBlockNode(expressions, dataType, node.Span);
    }

    private bool ShouldSkipBlockChild(SyntaxNode child)
    {
        return child is SyntaxErrorNode or SyntaxWithNode;
    }

    private SemanticNode Visit(SyntaxAttributeNode node)
    {
        var arguments = node
            .Arguments
            .Select(Next)
            .ToList();

        return new SemanticAttributeNode(node.Identifier, arguments, node.Span);
    }

    private SemanticNode Visit(SyntaxParameterNode node)
    {
        var dataType = Next(node.Type).DataType;

        return new SemanticParameterNode(node.Identifier, dataType, node.Span);
    }

    private SemanticNode Visit(SyntaxTypeNode node)
    {
        if (node.IsSlice)
        {
            var sliceType = new SliceDataType(NextSimpleType(node));

            return new SemanticTypeNode(sliceType, node.Span);
        }

        return new SemanticTypeNode(NextSimpleType(node), node.Span);
    }

    private IDataType NextSimpleType(SyntaxTypeNode node)
    {
        // Primitive
        Debug.Assert(node.TypeNames.Count > 0);
        var first = node.TypeNames.First();
        if (first.Kind != TokenKind.Identifier)
        {
            var primitiveKind = first.Kind switch
            {
                TokenKind.Void => Primitive.Void,
                TokenKind.Bool => Primitive.Bool,
                TokenKind.I8 => Primitive.Int8,
                TokenKind.I16 => Primitive.Int16,
                TokenKind.I32 => Primitive.Int32,
                TokenKind.I64 => Primitive.Int64,
                TokenKind.I128 => Primitive.Int128,
                TokenKind.ISize => Primitive.ISize,
                TokenKind.U8 => Primitive.Uint8,
                TokenKind.U16 => Primitive.Uint16,
                TokenKind.U32 => Primitive.Uint32,
                TokenKind.U64 => Primitive.Uint64,
                TokenKind.U128 => Primitive.Uint128,
                TokenKind.USize => Primitive.USize,
                TokenKind.F16 => Primitive.Float16,
                TokenKind.F32 => Primitive.Float32,
                TokenKind.F64 => Primitive.Float64,
                _ => throw new NotImplementedException(),
            };

            return new PrimitiveDataType(primitiveKind);
        }

        // Symbol
        var symbol = node.ResolvedSymbol;
        if (node.ResolvedSymbol == null)
        {
            var typeNames = node.TypeNames.Select(x => x.Value).ToList();
            symbol = _syntaxTree.File.ResolveStructure(typeNames);
        }

        if (symbol == null)
        {
            _diagnostics.ReportNotFound(node.TypeNames);
            throw Recover();
        }

        return new StructureDataType(symbol);
    }

    private SemanticNode Visit(SyntaxVariableDeclarationNode node)
    {
        var value = Next(node.Value);
        var dataType = value.DataType;
        if (node.Type != null)
        {
            dataType = Next(node.Type).DataType;
            value = TypeCheck(value, dataType);
        }

        var semanticNode = new SemanticVariableDeclarationNode(node.Identifier, value, dataType, node.Span);

        // Variable symbols are created in the analyser, since they can only be referenced
        // after they have been declared
        var scope = _syntaxTree.GetLocalScope(node);
        Debug.Assert(scope != null);
        scope.AddSymbol(new VariableSymbol(semanticNode));

        return semanticNode;
    }

    private SemanticNode Visit(SyntaxFunctionDeclarationNode node)
    {
        var isMain = node.Identifier.Value == "Main" &&
            _syntaxTree.GetEnclosingStructure(node)?.Identifier.Value == "Program";
        if (isMain && !node.IsStatic)
            _diagnostics.ReportNonStaticMainFunction(node.Span);

        var attributes = node
            .Attributes
            .Select(Next)
            .Cast<SemanticAttributeNode>()
            .ToList();

        var parameters = new List<SemanticParameterNode>();
        foreach (var parameter in node.Parameters)
        {
            var semanticParameter = (SemanticParameterNode)Next(parameter);
            parameters.Add(semanticParameter);

            if (node.Body?.Scope is LocalScope scope)
                scope.AddSymbol(new VariableSymbol(semanticParameter));
        }

        IDataType returnType;
        if (node.ReturnType != null)
        {
            returnType = Next(node.ReturnType).DataType;
        }
        else
        {
            returnType = PrimitiveDataType.Void;
        }

        var structure = _syntaxTree.GetEnclosingStructure(node);
        if (node.Identifier.Value == structure?.Identifier.Value)
            _diagnostics.ReportFunctionNameSameAsParentStructure(node.Identifier);

        if (structure is SyntaxProtocolDeclarationNode)
            node.Symbol!.IsVirtual = true;

        var parentClass = structure?
            .SubTypes
            .Select(x => x.ResolvedSymbol?.SyntaxDeclaration as SyntaxClassDeclarationNode)
            .Where(x => x != null)
            .FirstOrDefault();
        if (parentClass != null && node.IsOverride)
        {
            if (!parentClass.IsInheritable)
                _diagnostics.ReportBaseClassIsNotInheritable(node.Span, parentClass.Span);

            // TODO: If it does not exist in the parent, throw an error.
            // If it does exist, mark the base function as virtual
            var baseFunction = parentClass
                .Declarations
                .FirstOrDefault(x => (x as SyntaxFunctionDeclarationNode)?.Identifier.Value == node.Identifier.Value)
                    as SyntaxFunctionDeclarationNode;
            if (baseFunction == null)
            {
                _diagnostics.ReportBaseFunctionNotFound(node.Identifier);
            }
            else if (baseFunction.Symbol != null)
            {
                baseFunction.Symbol.IsVirtual = true;
            }
        }

        var body = node.Body == null
            ? null
            : (SemanticBlockNode)Next(node.Body);
        var semanticNode = new SemanticFunctionDeclarationNode(
            node.Identifier,
            parameters,
            returnType,
            body,
            node.IsStatic,
            node.IsOverride,
            node.Symbol!,
            node.Span
        )
        {
            Attributes = attributes,
        };

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticNode Visit(SyntaxClassDeclarationNode node)
    {
        if (node.SubTypes.Count > 1)
            _diagnostics.ReportMultipleInheritance(node.Span);

        StructureSymbol? inheritedClass = null;
        var implementedProtocols = new List<StructureSymbol>();
        foreach (var subTypeNode in node.SubTypes)
        {
            var subTypeDataType = Next(subTypeNode).DataType;
            if (subTypeDataType is StructureDataType structureDataType)
            {
                if (structureDataType.IsClass())
                {
                    inheritedClass = structureDataType.Symbol;
                }
                else if (structureDataType.IsProtocol())
                {
                    implementedProtocols.Add(structureDataType.Symbol);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                _diagnostics.ReportIncompatibleType("class", subTypeDataType, subTypeNode.Span);
            }
        }

        var functions = new List<SemanticFunctionDeclarationNode>();
        var fields = new List<SemanticFieldDeclarationNode>();
        foreach (var declaration in node.Declarations)
        {
            try
            {
                if (declaration is SyntaxFunctionDeclarationNode function)
                {
                    functions.Add((SemanticFunctionDeclarationNode)Next(function));
                }
                else if (declaration is SyntaxFieldDeclarationNode field)
                {
                    fields.Add((SemanticFieldDeclarationNode)Next(field));
                }
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        SemanticInitNode init;
        if (node.Init == null)
        {
            var emptyBlock = new SemanticBlockNode([], PrimitiveDataType.Void, _blankSpan);
            init = new SemanticInitNode([], null, emptyBlock, _blankSpan);
        }
        else
        {
            init = (SemanticInitNode)Next(node.Init);
        }

        var semanticNode = new SemanticClassDeclarationNode(
            node.Identifier,
            inheritedClass,
            implementedProtocols,
            init,
            functions,
            fields,
            node.IsInheritable,
            node.Symbol!,
            node.Span
        )
        {
            FieldStartIndex = CalculateFieldStartIndex(node),
        };

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private static int CalculateFieldStartIndex(SyntaxClassDeclarationNode classDeclarationNode)
    {
        var inheritedClassSyntax = classDeclarationNode.SubTypes
            .Select(x => x.ResolvedSymbol?.SyntaxDeclaration as SyntaxClassDeclarationNode)
            .FirstOrDefault();

        if (inheritedClassSyntax == null)
            return 0;

        var fieldCount = inheritedClassSyntax.Declarations.Count(x => x is SyntaxFieldDeclarationNode);

        return CalculateFieldStartIndex(inheritedClassSyntax) + fieldCount;
    }

    private SemanticNode Visit(SyntaxProtocolDeclarationNode node)
    {
        var functions = new List<SemanticFunctionDeclarationNode>();
        foreach (var declaration in node.Declarations)
        {
            try
            {
                if (declaration is SyntaxFunctionDeclarationNode function)
                    functions.Add((SemanticFunctionDeclarationNode)Next(function));
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        var semanticNode = new SemanticProtocolDeclarationNode(
            node.Identifier,
            functions,
            node.Symbol!,
            node.Span
        );

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticNode Visit(SyntaxModuleDeclarationNode node)
    {
        var functions = new List<SemanticFunctionDeclarationNode>();
        var fields = new List<SemanticFieldDeclarationNode>();
        foreach (var declaration in node.Declarations)
        {
            try
            {
                if (declaration is SyntaxFunctionDeclarationNode function)
                {
                    functions.Add((SemanticFunctionDeclarationNode)Next(function));
                }
                else if (declaration is SyntaxFieldDeclarationNode field)
                {
                    fields.Add((SemanticFieldDeclarationNode)Next(field));
                }
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        var semanticNode = new SemanticModuleDeclarationNode(
            node.Identifier,
            functions,
            fields,
            node.Symbol!,
            node.Span
        );

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticFieldDeclarationNode Visit(SyntaxFieldDeclarationNode node)
    {
        var attributes = node
            .Attributes
            .Select(Next)
            .Cast<SemanticAttributeNode>()
            .ToList();

        var dataType = Next(node.Type).DataType;
        var value = node.Value == null
            ? null
            : Next(node.Value);

        if (node.IsStatic && value is not (SemanticLiteralNode or null))
            _diagnostics.ReportNonConstantValueInStaticField(value.Span);

        var semanticNode = new SemanticFieldDeclarationNode(
            node.Identifier,
            value,
            node.IsStatic,
            dataType,
            node.Symbol!,
            node.Span
        )
        {
            Attributes = attributes,
        };

        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticInitNode Visit(SyntaxInitNode node)
    {
        var analysedParameters = new List<SemanticParameterNode>();
        var assignmentNodes = new List<SemanticAssignmentNode>();
        foreach (var parameter in node.Parameters)
        {
            var analysedParameter = Visit(parameter);
            analysedParameters.Add(analysedParameter);
            var parameterSymbol = new VariableSymbol(analysedParameter);

            var scope = (LocalScope)node.Body.Scope!;
            scope.AddSymbol(parameterSymbol);

            if (parameter.Type != null)
                continue;

            // References a field, so we need to insert assignment nodes
            var structure = _syntaxTree.GetEnclosingStructure(node);
            Debug.Assert(structure?.Symbol != null);
            var linkedSymbol = (FieldSymbol)structure.Scope.FindSymbol(parameter.Identifier.Value)!;
            var left = new SemanticFieldReferenceNode(
                parameter.Identifier,
                linkedSymbol,
                objectInstance: BuildSelfNode(structure.Symbol),
                analysedParameter.DataType
            );

            var right = new SemanticVariableReferenceNode(parameter.Identifier, parameterSymbol);
            var assignmentNode = new SemanticAssignmentNode(left, right, _blankSpan);
            assignmentNodes.Add(assignmentNode);
        }

        var body = (SemanticBlockNode)Next(node.Body);
        body.Expressions.AddRange(assignmentNodes);

        var baseCall = body.Expressions.FirstOrDefault() is SemanticKeywordValueNode { Keyword.Kind: TokenKind.Base } baseCallNode
            ? baseCallNode
            : null;
        if (baseCall != null)
            body.Expressions.RemoveAt(0);

        foreach (var expression in body.Expressions.Skip(1))
        {
            if (expression is SemanticKeywordValueNode { Keyword.Kind: TokenKind.Base } baseNode)
                _diagnostics.ReportMisplacedBaseCall(baseNode.Span);
        }

        return new SemanticInitNode(analysedParameters, baseCall, body, node.Span);
    }

    private SemanticParameterNode Visit(SyntaxInitParameterNode node)
    {
        if (node.Type != null)
        {
            var dataType = Next(node.Type).DataType;

            return new SemanticParameterNode(node.Identifier, dataType, node.Span);
        }

        Debug.Assert(node.LinkedField != null);
        var linkedDataType = Next(node.LinkedField.Type).DataType;

        return new SemanticParameterNode(node.Identifier, linkedDataType, node.Span);
    }
}
