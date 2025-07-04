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

    private SemanticNode Visit(SyntaxStatementNode node)
    {
        return Next(node.Expression);
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
        ValidateArguments(parameterTypes ?? [], arguments, node.Span);

        return new SemanticKeywordValueNode(
            node.Keyword,
            arguments,
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

            return new SemanticFunctionReferenceNode(
                identifier,
                functionSymbol2,
                objectInstance: BuildSelfNode(structure!.Symbol),
                dataType
            );
        }

        if (symbolFromStructure is FieldSymbol fieldSymbol2)
        {
            var dataType = Next(fieldSymbol2.SyntaxDeclaration.Type).DataType;

            Debug.Assert(structure?.Symbol != null);

            // TODO: Verify that the parent function isn't static since you can't reference
            // non-static fields from static functions
            return new SemanticFieldReferenceNode(
                identifier,
                fieldSymbol2,
                objectInstance: BuildSelfNode(structure.Symbol),
                dataType
            );
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

            if (!right.DataType.IsEquivalent(left.DataType))
            {
                _diagnostics.ReportIncompatibleType(left.DataType, right.DataType, right.Span);
                throw Recover();
            }
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
            if (!right.DataType.IsEquivalent(left.DataType))
            {
                _diagnostics.ReportIncompatibleType(left.DataType, right.DataType, right.Span);
                throw Recover();
            }
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

        if (!left.DataType.IsEquivalent(right.DataType))
        {
            _diagnostics.ReportIncompatibleType(left.DataType, right.DataType, node.Span);
        }

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
        ValidateArguments(parameterTypes, arguments, node.Span);

        var declarationReturnType = functionDataType.Symbol.SyntaxDeclaration.ReturnType;
        var dataType = declarationReturnType == null
            ? new PrimitiveDataType(Primitive.Void)
            : Next(declarationReturnType).DataType;

        return new SemanticCallNode(left, arguments, dataType, node.Span);
    }

    private SemanticNewNode Visit(SyntaxNewNode node)
    {
        var dataType = Next(node.Type).DataType;
        if (dataType is not StructureDataType structureDataType)
        {
            _diagnostics.ReportIncompatibleType("name of structure", dataType, node.Span);
            throw Recover();
        }

        var initNode = structureDataType.Symbol.SyntaxDeclaration switch
        {
            SyntaxClassDeclarationNode classNode => classNode.Init,
            _ => null,
        };

        if (initNode == null)
        {
            _diagnostics.ReportIncompatibleType("name of instantiable structure", dataType, node.Span);
            throw Recover();
        }

        var arguments = node.Arguments.Select(Next).ToList();
        var parameterTypes = initNode
            .Parameters
            .Select(Next)
            .Select(x => x.DataType)
            .ToList();
        ValidateArguments(parameterTypes, arguments, node.Span);

        return new SemanticNewNode(arguments, dataType, node.Span);
    }

    private void ValidateArguments(List<IDataType> parameterTypes, List<SemanticNode> arguments, TextSpan span)
    {
        if (parameterTypes.Count != arguments.Count)
            _diagnostics.ReportWrongNumberOfArguments(parameterTypes.Count, arguments.Count, span);

        foreach (var (parameterType, argument) in parameterTypes.Zip(arguments))
        {
            if (!argument.DataType.IsEquivalent(parameterType))
                _diagnostics.ReportIncompatibleType(parameterType, argument.DataType, argument.Span);
        }
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

        ValidateReturnType(enclosingFunction, value?.DataType, node.Span);

        return new SemanticReturnNode(value, node.Span);
    }

    private void ValidateReturnType(ISyntaxFunctionDeclaration enclosingFunction, IDataType? valueType, TextSpan span)
    {
        var functionReturnType = enclosingFunction.ReturnType == null
            ? null
            : Next(enclosingFunction.ReturnType).DataType;

        if (functionReturnType == null && valueType != null)
        {
            _diagnostics.ReportIncompatibleType(Primitive.Void, valueType, span);
        }
        else if (functionReturnType != null && valueType == null)
        {
            _diagnostics.ReportIncompatibleType(Primitive.Void, functionReturnType, span);
        }
        else if (functionReturnType != null && valueType != null && !functionReturnType.IsEquivalent(valueType))
        {
            _diagnostics.ReportIncompatibleType(functionReturnType, valueType, span);
        }
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

        IDataType dataType = new PrimitiveDataType(Primitive.Void);
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
                analysedLast = new SemanticReturnNode(analysedLast, node.Span);
                ValidateReturnType(enclosingFunction, analysedLast.DataType, node.Span);
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
        var semanticNode = new SemanticVariableDeclarationNode(node.Identifier, value, value.DataType, node.Span);

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
            returnType = new PrimitiveDataType(Primitive.Void);
        }

        var structure = _syntaxTree.GetEnclosingStructure(node);
        if (node.Identifier.Value == structure?.Identifier.Value)
            _diagnostics.ReportFunctionNameSameAsParentStructure(node.Identifier);

        var body = node.Body == null
            ? null
            : (SemanticBlockNode)Next(node.Body);
        var semanticNode = new SemanticFunctionDeclarationNode(
            node.Identifier,
            parameters,
            returnType,
            body,
            node.IsStatic,
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
        foreach (var subTypeNode in node.SubTypes)
        {
            var subTypeDataType = Next(subTypeNode).DataType;
            if (subTypeDataType is StructureDataType structureDataType && structureDataType.IsClass())
            {
                inheritedClass = structureDataType.Symbol;
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
            var emptyBlock = new SemanticBlockNode([], new PrimitiveDataType(Primitive.Void), _blankSpan);
            init = new SemanticInitNode([], null, emptyBlock, _blankSpan);
        }
        else
        {
            init = (SemanticInitNode)Next(node.Init);
        }

        var semanticNode = new SemanticClassDeclarationNode(
            node.Identifier,
            inheritedClass,
            init,
            functions,
            fields,
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
