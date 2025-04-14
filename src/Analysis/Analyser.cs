using System.Diagnostics;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;

namespace Caique.Analysis;

public class Analyser
{
    private SyntaxTree _syntaxTree;
    private DiagnosticReporter _diagnostics;

    private Analyser(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        _syntaxTree = syntaxTree;
        _diagnostics = compilationContext.DiagnosticReporter;
    }

    public static SemanticTree Analyse(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        var analyser = new Analyser(syntaxTree, compilationContext);
        Debug.Assert(syntaxTree.Root != null);
        var root = analyser.Next(syntaxTree.Root);

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
            SyntaxBlockNode blockNode => Visit(blockNode),
            SyntaxParameterNode parameterNode => Visit(parameterNode),
            SyntaxTypeNode typeNode => Visit(typeNode),
            SyntaxVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SyntaxFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SyntaxClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
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
                _diagnostics.ReportNotFound(node.IdentifierList[..^1]);
                throw Recover();
            }

            var identifierSymbol = structureSymbol.Node.Scope.FindSymbol(node.IdentifierList.Last().Value);
            if (identifierSymbol == null)
            {
                _diagnostics.ReportNotFound(node.IdentifierList);
                throw Recover();
            }

            if (identifierSymbol is FunctionSymbol symbol)
            {
                var dataType = new FunctionDataType(symbol);

                return new SemanticFunctionReferenceNode(node.IdentifierList.Last(), symbol, dataType);
            }

            throw new NotImplementedException();
        }

        var identifier = node.IdentifierList.Single();
        var variableSymbol = _syntaxTree.GetLocalScope(node)?.FindSymbol(identifier.Value);
        if (variableSymbol != null)
            return new SemanticVariableNode(identifier, variableSymbol);

        var symbolFromStructure = _syntaxTree.GetStructureScope(node)?.FindSymbol(identifier.Value);
        if (symbolFromStructure is FunctionSymbol functionSymbol)
        {
            var dataType = new FunctionDataType(functionSymbol);

            return new SemanticFunctionReferenceNode(identifier, functionSymbol, dataType);
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

        var declarationReturnType = functionDataType.Symbol.Declaration.ReturnType;
        var dataType = declarationReturnType == null
            ? new PrimitiveDataType(Primitive.Void)
            : Next(declarationReturnType).DataType;

        return new SemanticCallNode(left, arguments, dataType, node.Span);
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
                TokenKind.I8 => Primitive.Int8,
                TokenKind.I16 => Primitive.Int16,
                TokenKind.I32 => Primitive.Int32,
                TokenKind.I64 => Primitive.Int64,
                TokenKind.I128 => Primitive.Float128,
                TokenKind.F8 => Primitive.Float8,
                TokenKind.F16 => Primitive.Float16,
                TokenKind.F32 => Primitive.Float32,
                TokenKind.F64 => Primitive.Float64,
                TokenKind.F128 => Primitive.Float128,
                _ => throw new NotImplementedException(),
            };
            var primitiveDataType = new PrimitiveDataType(primitiveKind);

            return new SemanticTypeNode(primitiveDataType, node.Span);
        }

        // symbol
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
        var scope = (LocalScope)node.Body.Scope!;
        foreach (var parameter in node.Parameters)
        {
            var semanticParameter = (SemanticParameterNode)Next(parameter);
            parameters.Add(semanticParameter);
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

        var body = (SemanticBlockNode)Next(node.Body);

        return new SemanticFunctionDeclarationNode(node.Identifier, parameters, returnType, body, node.Span);
    }

    private SemanticNode Visit(SyntaxClassDeclarationNode node)
    {
        var declarations = new List<SemanticNode>();
        foreach (var declaration in node.Declarations)
        {
            try
            {
                declarations.Add(Next(declaration));
            }
            catch (AnalyserRecoveryException)
            {
                // Continue
            }
        }

        return new SemanticClassDeclarationNode(node.Identifier, declarations, node.Span);
    }
}
