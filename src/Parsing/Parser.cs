using System.Diagnostics;
using Caique.Lexing;
using Caique.Scope;

namespace Caique.Parsing;

public class Parser
{
    private readonly IEnumerator<Token> _enumerator;
    private readonly FileScope _fileScope;
    private readonly DiagnosticReporter _diagnostics;
    private Token? _previous;
    private Token? _current;
    private Token? _peek;
    private bool _reachedEnd;

    private Parser(IEnumerable<Token> tokens, FileScope fileScope, CompilationContext compilationContext)
    {
        _enumerator = tokens.GetEnumerator();
        _fileScope = fileScope;
        _diagnostics = compilationContext.DiagnosticReporter;

        _enumerator.MoveNext();
        _current = _enumerator.Current;
        _reachedEnd = !_enumerator.MoveNext();
        _peek = _enumerator.Current;
    }

    public static SyntaxTree Parse(string input, FileScope fileScope, CompilationContext compilationContext)
    {
        var syntaxTree = new SyntaxTree(input, fileScope);
        var tokens = Lexer.Lex(input, syntaxTree, compilationContext);
        var parser = new Parser(tokens, fileScope, compilationContext);
        var nodes = new List<SyntaxNode>();
        while (!parser._reachedEnd)
        {
            nodes.Add(parser.ParseTopLevelStatement());
        }

        var start = nodes.FirstOrDefault()?.Span.Start ?? TextPosition.Default(syntaxTree);
        var end = nodes.LastOrDefault()?.Span.End ?? TextPosition.Default(syntaxTree);
        var root = new SyntaxBlockNode(nodes, new TextSpan(start, end));
        syntaxTree.Initialise(root);

        return syntaxTree;
    }

    private SyntaxNode ParseTopLevelStatement()
    {
        return _current?.Kind switch
        {
            TokenKind.Class => ParseClass(),
            _ => ParseStatement(),
        };
    }

    private SyntaxStatementNode ParseStatement()
    {
        var expressionStatement = ParseExpression();

        // The last statement in a block doesn't need to end with a semicolon
        bool isReturnValue = Match(TokenKind.ClosedBrace);
        if (!isReturnValue)
            EatExpected(TokenKind.Semicolon, insert: true);

        return new SyntaxStatementNode(expressionStatement, isReturnValue);
    }

    private SyntaxNode ParseExpression()
    {
        return _current?.Kind switch
        {
            TokenKind.Let => ParseLet(),
            _ => ParseOr(),
        };
    }

    private SyntaxTypeNode ParseType()
    {
        var typeNames = new List<Token>();
        do
        {
            if (_current!.Kind != TokenKind.Identifier && (_current.Kind < TokenKind.Void || _current.Kind > TokenKind.F128))
            {
                Debug.Assert(_current != null);
                _diagnostics.ReportUnexpectedToken(_current, ["type name"]);

                throw Recover();
            }

            typeNames.Add(Eat());
        }
        while (AdvanceIf(TokenKind.ColonColon));

        return new SyntaxTypeNode(typeNames);
    }

    private SyntaxVariableDeclarationNode ParseLet()
    {
        var start = EatExpected(TokenKind.Let).Span;
        var identifier = EatExpected(TokenKind.Identifier);
        var type = AdvanceIf(TokenKind.Colon)
            ? ParseType()
            : null;
        EatExpected(TokenKind.Equals);
        var value = ParseExpression();

        return new SyntaxVariableDeclarationNode(identifier, type, value, start.Combine(value.Span));
    }

    private SyntaxParameterNode ParseParameter()
    {
        var identifier = EatExpected(TokenKind.Identifier);
        EatExpected(TokenKind.Colon);
        var type = ParseType();

        return new SyntaxParameterNode(identifier, type, identifier.Span.Combine(type.Span));
    }

    private SyntaxClassDeclarationNode ParseClass()
    {
        var start = EatExpected(TokenKind.Class).Span;
        var identifier = EatExpected(TokenKind.Identifier);
        EatExpected(TokenKind.OpenBrace);

        var declarations = new List<SyntaxNode>();
        var scope = new StructureScope(_fileScope.Namespace);
        while (!_reachedEnd && !Match(TokenKind.ClosedBrace))
        {
            try
            {
                var declaration = ParseStructureDeclaration(scope);
                declarations.Add(declaration);
            }
            catch (ParserRecoveryException ex)
            {
                declarations.Add(ex.ErrorNode);
            }
        }

        var end = EatExpected(TokenKind.ClosedBrace).Span;

        var node = new SyntaxClassDeclarationNode(identifier, declarations, scope, start.Combine(end));
        if (_fileScope.Namespace.FindType(node.Identifier.Value) != null)
            _diagnostics.ReportSymbolAlreadyExists(node.Identifier);

        var symbol = new StructureSymbol(identifier.Value, node, _fileScope.Namespace);
        node.Symbol = symbol;
        _fileScope.Namespace.AddSymbol(symbol);

        return node;
    }

    private SyntaxNode ParseStructureDeclaration(StructureScope scope)
    {
        if (Match(TokenKind.Fn))
            return ParseFunction(scope);

        throw new NotImplementedException();
    }

    private SyntaxFunctionDeclarationNode ParseFunction(StructureScope? scope = null)
    {
        var start = EatExpected(TokenKind.Fn).Span;
        var identifier = EatExpected(TokenKind.Identifier);
        var parameters = ParseParameters();
        var returnType = AdvanceIf(TokenKind.Colon)
            ? ParseType()
            : null;
        var body = ParseBlock();

        var node = new SyntaxFunctionDeclarationNode(
            identifier,
            parameters,
            returnType,
            body,
            start.Combine(body.Span)
        );

        if (scope?.FindSymbol(node.Identifier.Value) != null)
            _diagnostics.ReportSymbolAlreadyExists(node.Identifier);

        var symbol = new FunctionSymbol(node);
        node.Symbol = symbol;
        scope?.AddSymbol(symbol);

        return node;
    }

    private List<SyntaxParameterNode> ParseParameters()
    {
        EatExpected(TokenKind.OpenParenthesis);

        var parameters = new List<SyntaxParameterNode>();
        if (Match(TokenKind.Identifier))
            parameters.Add(ParseParameter());

        while (!_reachedEnd && AdvanceIf(TokenKind.Comma))
            parameters.Add(ParseParameter());

        AdvanceIf(TokenKind.Comma);
        EatExpected(TokenKind.ClosedParenthesis);

        return parameters;
    }

    private SyntaxNode ParseOr()
    {
        var left = ParseAnd();
        while (Match(TokenKind.PipePipe))
        {
            var op = Eat().Kind;
            var right = ParseAnd();
            left = new SyntaxBinaryNode(left, op, right);
        }

        return left;
    }

    private SyntaxNode ParseAnd()
    {
        var left = ParseComparison();
        while (Match(TokenKind.AmpersandAmpersand))
        {
            var op = Eat().Kind;
            var right = ParseComparison();
            left = new SyntaxBinaryNode(left, op, right);
        }

        return left;
    }

    private SyntaxNode ParseComparison()
    {
        var left = ParseAdditive();
        while (
            Match(
                TokenKind.EqualsEquals,
                TokenKind.NotEquals,
                TokenKind.Greater,
                TokenKind.GreaterEquals,
                TokenKind.Less,
                TokenKind.LessEquals
            )
        )
        {
            var op = Eat().Kind;
            var right = ParseAdditive();
            left = new SyntaxBinaryNode(left, op, right);
        }

        return left;
    }

    private SyntaxNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Match(TokenKind.Plus, TokenKind.Minus))
        {
            var op = Eat().Kind;
            var right = ParseMultiplicative();
            left = new SyntaxBinaryNode(left, op, right);
        }

        return left;
    }

    private SyntaxNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Match(TokenKind.Star, TokenKind.Slash))
        {
            var op = Eat().Kind;
            var right = ParseUnary();
            left = new SyntaxBinaryNode(left, op, right);
        }

        return left;
    }

    private SyntaxNode ParseUnary()
    {
        if (Match(TokenKind.Minus, TokenKind.Exclamation))
        {
            var op = Eat();
            var value = ParseCall();

            return new SyntaxUnaryNode(op.Kind, value, op.Span.Combine(value.Span));
        }

        return ParseCall();
    }

    private SyntaxNode ParseCall()
    {
        var left = ParsePrimary();
        if (Match(TokenKind.OpenParenthesis))
        {
            var arguments = ParseArguments();

            return new SyntaxCallNode(left, arguments, left.Span.Combine(_previous!.Span));
        }

        return left;
    }

    private SyntaxNode ParsePrimary()
    {
        if (Match(TokenKind.OpenBrace))
            return ParseBlock();

        if (Match(TokenKind.NumberLiteral, TokenKind.StringLiteral))
            return ParseLiteral();

        if (Match(TokenKind.Identifier))
            return ParseIdentifier();

        _diagnostics.ReportUnexpectedToken(_current!);
        throw Recover();
    }

    private ParserRecoveryException Recover()
    {
        var start = _current == null
            ? _previous!.Span
            : Eat().Span;
        while (!Match(TokenKind.Semicolon, TokenKind.ClosedBrace, TokenKind.EndOfFile))
            Eat();

        // Only semicolons should be consumed since they would probably belong to the same statement
        if (Match(TokenKind.Semicolon))
            Eat();

        var end = _previous?.Span ?? _current!.Span;
        var node = new SyntaxErrorNode(start.Combine(end));

        return new ParserRecoveryException(node);
    }

    private SyntaxBlockNode ParseBlock()
    {
        var start = EatExpected(TokenKind.OpenBrace).Span;

        var expressions = new List<SyntaxNode>();
        while (!_reachedEnd && !Match(TokenKind.ClosedBrace))
        {
            try
            {
                expressions.Add(ParseStatement());
            }
            catch (ParserRecoveryException recovery)
            {
                expressions.Add(recovery.ErrorNode);
            }
        }

        var end = EatExpected(TokenKind.ClosedBrace).Span;

        return new SyntaxBlockNode(expressions, start.Combine(end));
    }

    private SyntaxLiteralNode ParseLiteral()
    {
        var token = EatExpected(TokenKind.NumberLiteral, TokenKind.StringLiteral);

        return new SyntaxLiteralNode(token);
    }

    private SyntaxIdentifierNode ParseIdentifier()
    {
        var identifier = EatExpected(TokenKind.Identifier);

        return new SyntaxIdentifierNode(identifier);
    }

    private List<SyntaxNode> ParseArguments()
    {
        EatExpected(TokenKind.OpenParenthesis);

        var arguments = new List<SyntaxNode>();
        while (!_reachedEnd && !Match(TokenKind.ClosedParenthesis))
        {
            arguments.Add(ParseExpression());
            if (!AdvanceIf(TokenKind.Comma))
                break;
        }

        EatExpected(TokenKind.ClosedParenthesis);

        return arguments;
    }

    private bool Match(params TokenKind[] kinds)
    {
        return _current != null && kinds.Contains(_current.Kind);
    }

    private bool AdvanceIf(TokenKind kind)
    {
        if (Match(kind))
        {
            Eat();

            return true;
        }

        return false;
    }

    private Token Eat()
    {
        _previous = _current;
        _reachedEnd = !_enumerator.MoveNext() || _enumerator.Current?.Kind == TokenKind.EndOfFile;
        _current = _peek;
        _peek = _enumerator.Current;

        return _previous!;
    }

    private Token EatExpected(TokenKind kind, bool insert)
    {
        if (insert && !Match(kind))
        {
            Debug.Assert(_current != null);
            if (_current.Kind == TokenKind.EndOfFile)
            {
                _diagnostics.ReportUnexpectedEnd(_current);
            }
            else
            {
                _diagnostics.ReportUnexpectedToken(_current, [kind.ToString()]);
            }

            return new Token(kind, string.Empty, _current.Span);
        }

        return EatExpected(kind);
    }

    private Token EatExpected(params TokenKind[] kinds)
    {
        if (!Match(kinds))
        {
            Debug.Assert(_current != null);
            var expected = kinds.Select(x => x.ToString()).ToList();
            _diagnostics.ReportUnexpectedToken(_current, expected);

            throw Recover();
        }

        return Eat();
    }
}
