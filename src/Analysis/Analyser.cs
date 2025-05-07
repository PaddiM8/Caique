using System.Diagnostics;
using Caique.Backend;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;

namespace Caique.Analysis;

public class Analyser
{
    private readonly TextSpan _blankSpan;
    private readonly SyntaxTree _syntaxTree;
    private readonly DiagnosticReporter _diagnostics;

    private Analyser(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        _syntaxTree = syntaxTree;
        _diagnostics = compilationContext.DiagnosticReporter;
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
            SyntaxLiteralNode literalNode => Visit(literalNode),
            SyntaxIdentifierNode identifierNode => Visit(identifierNode),
            SyntaxUnaryNode unaryNode => Visit(unaryNode),
            SyntaxBinaryNode binaryNode => Visit(binaryNode),
            SyntaxCallNode callNode => Visit(callNode),
            SyntaxNewNode newNode => Visit(newNode),
            SyntaxReturnNode returnNode => Visit(returnNode),
            SyntaxBlockNode blockNode => Visit(blockNode),
            SyntaxParameterNode parameterNode => Visit(parameterNode),
            SyntaxTypeNode typeNode => Visit(typeNode),
            SyntaxVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SyntaxFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SyntaxClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            SyntaxFieldDeclarationNode fieldDeclarationNode => Visit(fieldDeclarationNode),
            SyntaxInitNode initNode => Visit(initNode),
            _ => throw new NotImplementedException(),
        };
    }

    private AnalyserRecoveryException Recover()
    {
        return new AnalyserRecoveryException();
    }

    private SemanticNode Visit(SyntaxStatementNode node)
    {
        return Next(node.Expression);
    }

    private SemanticNode Visit(SyntaxLiteralNode node)
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
            var dataType = new PrimitiveDataType(Primitive.String);

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
            var structureSymbol = _syntaxTree.Namespace.ResolveStructure(namespaceNames);
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

                return new SemanticFunctionReferenceNode(node.IdentifierList.Last(), functionSymbol, dataType);
            }

            if (identifierSymbol is FieldSymbol fieldSymbol)
            {
                if (!fieldSymbol.SyntaxDeclaration.IsStatic)
                {
                    _diagnostics.ReportNonStaticSymbolReferencedAsStatic(node.IdentifierList.Last());
                }

                var dataType = Next(fieldSymbol.SyntaxDeclaration.Type).DataType;

                return new SemanticFieldReferenceNode(
                    node.IdentifierList.Last(),
                    fieldSymbol,
                    explicitObjectInstance: null,
                    isStatic: true,
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
        var symbolFromStructure = structure?.Scope.FindSymbol(identifier.Value);
        if (symbolFromStructure is FunctionSymbol functionSymbol2)
        {
            var dataType = new FunctionDataType(functionSymbol2);
            if (functionSymbol2.SyntaxDeclaration.IsStatic)
            {
                _diagnostics.ReportStaticSymbolReferencedAsNonStatic(identifier);
            }

            return new SemanticFunctionReferenceNode(identifier, functionSymbol2, dataType);
        }

        if (symbolFromStructure is FieldSymbol fieldSymbol2)
        {
            var dataType = Next(fieldSymbol2.SyntaxDeclaration.Type).DataType;
            if (fieldSymbol2.SyntaxDeclaration.IsStatic)
            {
                _diagnostics.ReportStaticSymbolReferencedAsNonStatic(identifier);
            }

            Debug.Assert(structure?.Symbol != null);

            // TODO: Verify that the parent function isn't static since you can't reference
            // non-static fields from static functions
            return new SemanticFieldReferenceNode(
                identifier,
                fieldSymbol2,
                explicitObjectInstance: null,
                isStatic: false,
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
        if (parameterTypes.Count != arguments.Count)
            _diagnostics.ReportWrongNumberOfArguments(parameterTypes.Count, arguments.Count, node.Span);

        foreach (var (parameterType, argument) in parameterTypes.Zip(arguments))
        {
            if (!argument.DataType.IsEquivalent(parameterType))
                _diagnostics.ReportIncompatibleType(parameterType, argument.DataType, argument.Span);
        }

        var declarationReturnType = functionDataType.Symbol.SyntaxDeclaration.ReturnType;
        var dataType = declarationReturnType == null
            ? new PrimitiveDataType(Primitive.Void)
            : Next(declarationReturnType).DataType;

        return new SemanticCallNode(left, arguments, dataType, node.Span);
    }

    private SemanticNewNode Visit(SyntaxNewNode node)
    {
        var dataType = Next(node.Type).DataType;
        var arguments = node.Arguments.Select(Next).ToList();

        return new SemanticNewNode(arguments, dataType, node.Span);
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

        var functionReturnType = enclosingFunction.ReturnType == null
            ? null
            : Next(enclosingFunction.ReturnType).DataType;
        if (functionReturnType == null && value != null)
        {
            _diagnostics.ReportIncompatibleType(Primitive.Void, value.DataType, node.Span);
        }
        else if (functionReturnType != null && value == null)
        {
            _diagnostics.ReportIncompatibleType(Primitive.Void, functionReturnType, node.Span);
        }
        else if (functionReturnType != null && value != null && !functionReturnType.IsEquivalent(value.DataType))
        {
            _diagnostics.ReportIncompatibleType(functionReturnType, value.DataType, node.Span);
        }


        return new SemanticReturnNode(value, node.Span);
    }

    private SemanticNode Visit(SyntaxBlockNode node)
    {
        var expressions = new List<SemanticNode>();
        foreach (var child in node.Expressions)
        {
            if (child is SyntaxErrorNode)
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

        var dataType = expressions.LastOrDefault()?.DataType
            ?? new PrimitiveDataType(Primitive.Void);

        return new SemanticBlockNode(expressions, dataType, node.Span);
    }

    private SemanticNode Visit(SyntaxParameterNode node)
    {
        var dataType = Next(node.Type).DataType;

        return new SemanticParameterNode(node.Identifier, dataType, node.Span);
    }

    private SemanticNode Visit(SyntaxTypeNode node)
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
                TokenKind.String => Primitive.String,
                TokenKind.I8 => Primitive.Int8,
                TokenKind.I16 => Primitive.Int16,
                TokenKind.I32 => Primitive.Int32,
                TokenKind.I64 => Primitive.Int64,
                TokenKind.I128 => Primitive.Int128,
                TokenKind.F16 => Primitive.Float16,
                TokenKind.F32 => Primitive.Float32,
                TokenKind.F64 => Primitive.Float64,
                _ => throw new NotImplementedException(),
            };
            var primitiveDataType = new PrimitiveDataType(primitiveKind);

            return new SemanticTypeNode(primitiveDataType, node.Span);
        }

        // Symbol
        var symbol = node.ResolvedSymbol;
        if (node.ResolvedSymbol == null)
        {
            var typeNames = node.TypeNames.Select(x => x.Value).ToList();
            symbol = _syntaxTree.Namespace.ResolveStructure(typeNames);
        }

        if (symbol == null)
        {
            _diagnostics.ReportNotFound(node.TypeNames);
            throw Recover();
        }

        var dataType = new StructureDataType(symbol);

        return new SemanticTypeNode(dataType, node.Span);
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
        );
        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticNode Visit(SyntaxClassDeclarationNode node)
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
                else if (declaration is SyntaxFunctionDeclarationNode field)
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
            init = new SemanticInitNode([], emptyBlock, _blankSpan);
        }
        else
        {
            init = (SemanticInitNode)Next(node.Init);
        }

        var semanticNode = new SemanticClassDeclarationNode(
            node.Identifier,
            init,
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
        var dataType = Next(node).DataType;
        var value = node.Value == null
            ? null
            : Next(node);

        var semanticNode = new SemanticFieldDeclarationNode(
            node.Identifier,
            value,
            node.IsStatic,
            dataType,
            node.Symbol!,
            node.Span
        );
        node.Symbol!.SemanticDeclaration = semanticNode;

        return semanticNode;
    }

    private SemanticInitNode Visit(SyntaxInitNode node)
    {
        var analysedParameters = new List<SemanticParameterNode>();
        var assignmentNodes = new List<SemanticAssignmentNode>();
        foreach (var parameter in node.Parameters)
        {
            var (analysedParameter, hasType) = NextInitParameter(parameter);
            analysedParameters.Add(analysedParameter);
            var parameterSymbol = new VariableSymbol(analysedParameter);

            var scope = (LocalScope)node.Body.Scope!;
            scope.AddSymbol(parameterSymbol);

            if (hasType)
                continue;

            // References a field, so we need to insert assignment nodes
            var linkedSymbol = _syntaxTree.GetStructureScope(node)!.FindSymbol(parameter.Identifier.Value)!;
            var assignmentNode = new SemanticAssignmentNode(
                linkedSymbol,
                new SemanticVariableReferenceNode(parameter.Identifier, parameterSymbol),
                _blankSpan
            );
            assignmentNodes.Add(assignmentNode);
        }

        var body = (SemanticBlockNode)Next(node.Body);
        body.Expressions.InsertRange(0, assignmentNodes);

        return new SemanticInitNode(analysedParameters, body, node.Span);
    }

    private (SemanticParameterNode node, bool hasType) NextInitParameter(SyntaxInitParameterNode node)
    {
        if (node.Type != null)
        {
            var dataType = Next(node.Type).DataType;

            return (new SemanticParameterNode(node.Identifier, dataType, node.Span), hasType: true);
        }

        Debug.Assert(node.LinkedField != null);
        var linkedDataType = Next(node.LinkedField.Type).DataType;

        return (new SemanticParameterNode(node.Identifier, linkedDataType, node.Span), hasType: false);
    }
}
