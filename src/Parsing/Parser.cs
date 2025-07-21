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

    public static SyntaxTree Parse(FileScope fileScope, CompilationContext compilationContext)
    {
        var syntaxTree = new SyntaxTree(fileScope.FileContent, fileScope);
        var tokens = Lexer.Lex(fileScope.FileContent, syntaxTree, compilationContext);
        var parser = new Parser(tokens, fileScope, compilationContext);
        var nodes = new List<SyntaxNode>();
        while (!parser._reachedEnd)
        {
            var node = parser.ParseTopLevelStatement();
            if (node != null)
                nodes.Add(node);
        }

        var start = nodes.FirstOrDefault()?.Span.Start ?? TextPosition.Default(syntaxTree);
        var end = nodes.LastOrDefault()?.Span.End ?? TextPosition.Default(syntaxTree);
        var root = new SyntaxBlockNode(
            nodes,
            isArrowBlock: false,
            new TextSpan(start, end)
        );

        syntaxTree.Initialise(root);

        return syntaxTree;
    }

    private SyntaxNode? ParseTopLevelStatement()
    {
        bool isInheritable = AdvanceIf(TokenKind.Inheritable);

        try
        {
            return _current?.Kind switch
            {
                TokenKind.With => ParseWith(),
                TokenKind.Class => ParseClass(isInheritable),
                TokenKind.Protocol => ParseProtocol(),
                TokenKind.Module => ParseModule(),
                TokenKind.Enum => ParseEnum(),
                _ => ParseStatement(),
            };
        }
        catch (ParserRecoveryException)
        {
            return null;
        }
    }

    private SyntaxStatementNode ParseStatement(bool isArrowBlock = false)
    {
        var expressionStatement = ParseExpression();

        // The last statement in a block doesn't need to end with a semicolon (except for return statements)
        bool hasTrailingSemicolon = false;
        var isBlockNode = expressionStatement is SyntaxBlockNode or SyntaxIfNode;
        var isReturnValue = isArrowBlock || Match(TokenKind.ClosedBrace) || isBlockNode;
        if (!isReturnValue || expressionStatement is SyntaxReturnNode)
        {
            EatExpected(TokenKind.Semicolon, insert: true);
            hasTrailingSemicolon = true;
        }

        return new SyntaxStatementNode(expressionStatement, hasTrailingSemicolon);
    }

    private SyntaxNode ParseExpression()
    {
        return _current?.Kind switch
        {
            TokenKind.Let or TokenKind.Var => ParseVariableDeclaration(),
            _ => ParseAssignment(),
        };
    }

    private SyntaxTypeNode ParseType()
    {
        var start = _current?.Span;
        var isSlice = AdvanceIf(TokenKind.OpenBracket);

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
        while (AdvanceIf(TokenKind.Colon));

        if (isSlice)
            EatExpected(TokenKind.ClosedBracket);

        var typeArguments = new List<SyntaxTypeNode>();
        if (AdvanceIf(TokenKind.OpenBracket))
        {
            do
            {
                typeArguments.Add(ParseType());
            }
            while (AdvanceIf(TokenKind.Comma));

            EatExpected(TokenKind.ClosedBracket);
        }

        var span = start!.Combine(_previous!.Span);

        return new SyntaxTypeNode(typeNames, isSlice, typeArguments, span);
    }

    private SyntaxVariableDeclarationNode ParseVariableDeclaration()
    {
        var keyword = EatExpected(TokenKind.Let, TokenKind.Var);
        var identifier = EatExpected(TokenKind.Identifier);
        var type = Match(TokenKind.Equals)
            ? null
            : ParseType();
        EatExpected(TokenKind.Equals);
        var value = ParseExpression();

        return new SyntaxVariableDeclarationNode(
            isMutable: keyword.Kind == TokenKind.Var,
            identifier,
            type,
            value,
            keyword.Span.Combine(value.Span)
        );
    }

    private SyntaxParameterNode ParseParameter()
    {
        var identifier = EatExpected(TokenKind.Identifier);
        var type = ParseType();

        return new SyntaxParameterNode(identifier, type, identifier.Span.Combine(type.Span));
    }

    private SyntaxWithNode ParseWith()
    {
        var start = EatExpected(TokenKind.With).Span;
        var identifiers = new List<Token>();
        do
        {
            var identifier = EatExpected(TokenKind.Identifier);
            identifiers.Add(identifier);
        }
        while (AdvanceIf(TokenKind.Colon));

        EatExpected(TokenKind.Semicolon);

        return new SyntaxWithNode(identifiers, start.Combine(identifiers.First().Span));
    }

    private SyntaxClassDeclarationNode ParseClass(bool isInheritable)
    {
        var start = EatExpected(TokenKind.Class).Span;
        var identifier = EatExpected(TokenKind.Identifier);

        var subTypes = new List<SyntaxTypeNode>();
        if (AdvanceIf(TokenKind.Colon))
        {
            do
            {
                subTypes.Add(ParseType());
            }
            while (AdvanceIf(TokenKind.Comma));
        }

        var typeParameters = new List<Token>();
        if (Match(TokenKind.OpenBracket))
            typeParameters = ParseTypeParameters();

        EatExpected(TokenKind.OpenBrace);

        var (declarations, constructor, scope) = ParseStructureBody(identifier);
        var end = EatExpected(TokenKind.ClosedBrace).Span;

        var node = new SyntaxClassDeclarationNode(
            identifier,
            subTypes,
            constructor,
            declarations,
            isInheritable,
            scope,
            start.Combine(end)
        );
        if (_fileScope.Namespace.FindSymbol(node.Identifier.Value) != null)
            _diagnostics.ReportSymbolAlreadyExists(node.Identifier);

        foreach (var typeParameter in typeParameters)
            scope.AddTypeParameter(new TypeSymbol(typeParameter.Value));

        var symbol = new StructureSymbol(identifier.Value, node, _fileScope.Namespace);
        node.Symbol = symbol;
        _fileScope.Namespace.AddSymbol(symbol);

        return node;
    }

    private List<Token> ParseTypeParameters()
    {
        EatExpected(TokenKind.OpenBracket);

        var typeParameters = new List<Token>();
        do
        {
            typeParameters.Add(EatExpected(TokenKind.Identifier));
        }
        while (AdvanceIf(TokenKind.Comma));

        EatExpected(TokenKind.ClosedBracket);

        return typeParameters;
    }

    private (List<SyntaxNode>, SyntaxInitNode? constructor, StructureScope scope) ParseStructureBody(Token identifier)
    {
        var declarations = new List<SyntaxNode>();
        var scope = new StructureScope(_fileScope.Namespace);
        SyntaxInitNode? constructor = null;
        while (!_reachedEnd && !Match(TokenKind.ClosedBrace))
        {
            try
            {
                var declaration = ParseStructureDeclaration(scope);
                if (declaration is SyntaxInitNode initNode)
                {
                    if (constructor != null)
                        _diagnostics.ReportConstructorAlreadyExists(identifier, declaration.Span);

                    constructor = initNode;
                }
                else
                {
                    declarations.Add(declaration);
                }
            }
            catch (ParserRecoveryException ex)
            {
                declarations.Add(ex.ErrorNode);
            }
        }

        return (declarations, constructor, scope);
    }

    private SyntaxNode ParseStructureDeclaration(StructureScope scope)
    {
        var attributes = new List<SyntaxAttributeNode>();
        while (Match(TokenKind.Hash))
            attributes.Add(ParseAttribute());

        bool isPublic = AdvanceIf(TokenKind.Pub);
        bool isStatic = AdvanceIf(TokenKind.Static);
        bool isOverride = AdvanceIf(TokenKind.Override);
        if (Match(TokenKind.Func))
            return ParseFunction(isPublic, isStatic, isOverride, attributes, scope);

        if (_current is { Kind: TokenKind.Identifier, Value: "init" })
            return ParseInit();

        if (Match(TokenKind.Let, TokenKind.Var))
            return ParseField(isPublic, isStatic, attributes, scope);

        throw Recover();
    }

    private SyntaxProtocolDeclarationNode ParseProtocol()
    {
        var start = EatExpected(TokenKind.Protocol).Span;
        var identifier = EatExpected(TokenKind.Identifier);

        var subTypes = new List<SyntaxTypeNode>();
        if (AdvanceIf(TokenKind.Colon))
        {
            do
            {
                subTypes.Add(ParseType());
            }
            while (AdvanceIf(TokenKind.Comma));
        }

        EatExpected(TokenKind.OpenBrace);
        var (declarations, scope) = ParseProtocolBody();

        var end = EatExpected(TokenKind.ClosedBrace).Span;

        var node = new SyntaxProtocolDeclarationNode(
            identifier,
            subTypes,
            declarations,
            scope,
            start.Combine(end)
        );
        if (_fileScope.Namespace.FindSymbol(node.Identifier.Value) != null)
            _diagnostics.ReportSymbolAlreadyExists(node.Identifier);

        var symbol = new StructureSymbol(identifier.Value, node, _fileScope.Namespace);
        node.Symbol = symbol;
        _fileScope.Namespace.AddSymbol(symbol);

        return node;
    }

    private (List<SyntaxNode>, StructureScope scope) ParseProtocolBody()
    {
        var declarations = new List<SyntaxNode>();
        var scope = new StructureScope(_fileScope.Namespace);
        while (!_reachedEnd && !Match(TokenKind.ClosedBrace))
        {
            try
            {
                var declaration = ParseProtocolDeclaration(scope);
                declarations.Add(declaration);
            }
            catch (ParserRecoveryException ex)
            {
                declarations.Add(ex.ErrorNode);
            }
        }

        return (declarations, scope);
    }

    private SyntaxNode ParseProtocolDeclaration(StructureScope scope)
    {
        var attributes = new List<SyntaxAttributeNode>();
        while (Match(TokenKind.Hash))
            attributes.Add(ParseAttribute());

        if (Match(TokenKind.Func))
        {
            var function = ParseFunction(isPublic: true, isStatic: false, isOverride: false, attributes, scope);
            if (function.Body != null)
                _diagnostics.ReportBodyInProtocol(function.Span);

            return function;
        }

        throw Recover();
    }

    private SyntaxModuleDeclarationNode ParseModule()
    {
        var start = EatExpected(TokenKind.Module).Span;
        var identifier = EatExpected(TokenKind.Identifier);

        EatExpected(TokenKind.OpenBrace);
        var (declarations, scope) = ParseModuleBody();

        var end = EatExpected(TokenKind.ClosedBrace).Span;

        var node = new SyntaxModuleDeclarationNode(
            identifier,
            declarations,
            scope,
            start.Combine(end)
        );
        if (_fileScope.Namespace.FindSymbol(node.Identifier.Value) != null)
            _diagnostics.ReportSymbolAlreadyExists(node.Identifier);

        var symbol = new StructureSymbol(identifier.Value, node, _fileScope.Namespace);
        node.Symbol = symbol;
        _fileScope.Namespace.AddSymbol(symbol);

        return node;
    }

    private SyntaxEnumDeclarationNode ParseEnum()
    {
        var start = EatExpected(TokenKind.Enum).Span;
        var identifier = EatExpected(TokenKind.Identifier);

        SyntaxTypeNode? type = null;
        if (AdvanceIf(TokenKind.Colon))
            type = ParseType();

        EatExpected(TokenKind.OpenBrace);

        var members = new List<SyntaxEnumMemberNode>();
        do
        {
            if (Match(TokenKind.ClosedBrace))
                break;

            var memberIdentifier = EatExpected(TokenKind.Identifier);
            var span = memberIdentifier.Span;

            SyntaxNode? value = null;
            if (AdvanceIf(TokenKind.Equals))
            {
                value = ParseExpression();
                span = span.Combine(value.Span);
            }

            var member = new SyntaxEnumMemberNode(memberIdentifier, value, span);
            members.Add(member);
        }
        while (!_reachedEnd && AdvanceIf(TokenKind.Comma));

        var end = EatExpected(TokenKind.ClosedBrace).Span;
        var node = new SyntaxEnumDeclarationNode(
            identifier,
            members,
            type,
            start.Combine(end)
        );

        var symbol = new EnumSymbol(identifier.Value, node, _fileScope.Namespace);
        node.Symbol = symbol;
        _fileScope.Namespace.AddSymbol(symbol);

        return node;
    }

    private (List<SyntaxNode>, StructureScope scope) ParseModuleBody()
    {
        var declarations = new List<SyntaxNode>();
        var scope = new StructureScope(_fileScope.Namespace);
        while (!_reachedEnd && !Match(TokenKind.ClosedBrace))
        {
            try
            {
                var declaration = ParseModuleDeclaration(scope);
                declarations.Add(declaration);
            }
            catch (ParserRecoveryException ex)
            {
                declarations.Add(ex.ErrorNode);
            }
        }

        return (declarations, scope);
    }

    private SyntaxNode ParseModuleDeclaration(StructureScope scope)
    {
        var attributes = new List<SyntaxAttributeNode>();
        while (Match(TokenKind.Hash))
            attributes.Add(ParseAttribute());

        var isPublic = AdvanceIf(TokenKind.Pub);
        if (Match(TokenKind.Func))
            return ParseFunction(isPublic, isStatic: true, isOverride: false, attributes, scope);

        if (Match(TokenKind.Let, TokenKind.Var))
            return ParseField(isPublic, isStatic: true, attributes, scope);

        throw Recover();
    }

    private SyntaxAttributeNode ParseAttribute()
    {
        var start = EatExpected(TokenKind.Hash).Span;
        var identifier = EatExpected(TokenKind.Identifier);
        var arguments = Match(TokenKind.OpenParenthesis)
            ? ParseArguments()
            : [];
        var span = start.Combine(arguments.LastOrDefault()?.Span ?? identifier.Span);

        return new SyntaxAttributeNode(identifier, arguments, span);
    }

    private SyntaxFunctionDeclarationNode ParseFunction(
        bool isPublic,
        bool isStatic,
        bool isOverride,
        List<SyntaxAttributeNode> attributes,
        StructureScope? scope = null
    )
    {
        var start = EatExpected(TokenKind.Func).Span;
        var identifier = EatExpected(TokenKind.Identifier);
        var parameters = ParseParameters();
        var returnType = Match(TokenKind.OpenBrace, TokenKind.Semicolon)
            ? null
            : ParseType();
        var body = AdvanceIf(TokenKind.Semicolon)
            ? null
            : ParseBlock();

        var node = new SyntaxFunctionDeclarationNode(
            identifier,
            parameters,
            returnType,
            body,
            isPublic,
            isStatic,
            isOverride,
            start.Combine(_previous!.Span)
        )
        {
            Attributes = attributes,
        };

        if (scope?.FindSymbol(node.Identifier.Value) != null)
            _diagnostics.ReportSymbolAlreadyExists(node.Identifier);

        var symbol = new FunctionSymbol(node);
        node.Symbol = symbol;
        scope?.AddSymbol(symbol);

        return node;
    }

    private SyntaxInitNode ParseInit()
    {
        var start = EatExpected(TokenKind.Identifier).Span;
        var parameters = ParseInitParameters();
        var body = ParseBlock();

        var node = new SyntaxInitNode(
            parameters,
            body,
            start.Combine(body.Span)
        );

        return node;
    }

    private List<SyntaxInitParameterNode> ParseInitParameters()
    {
        EatExpected(TokenKind.OpenParenthesis);

        var parameters = new List<SyntaxInitParameterNode>();

        do
        {
            if (Match(TokenKind.ClosedParenthesis))
                break;

            var identifier = EatExpected(TokenKind.Identifier);
            SyntaxTypeNode? type = null;
            if (!Match(TokenKind.Comma, TokenKind.ClosedParenthesis))
                type = ParseType();

            parameters.Add(new SyntaxInitParameterNode(identifier, type));
        }
        while (!_reachedEnd && AdvanceIf(TokenKind.Comma));

        EatExpected(TokenKind.ClosedParenthesis);

        return parameters;
    }

    private SyntaxFieldDeclarationNode ParseField(
        bool isPublic,
        bool isStatic,
        List<SyntaxAttributeNode> attributes,
        StructureScope scope
    )
    {
        var keyword = EatExpected(TokenKind.Let, TokenKind.Var);
        var identifier = EatExpected(TokenKind.Identifier);
        var type = ParseType();

        SyntaxNode? value = null;
        if (AdvanceIf(TokenKind.Equals))
            value = ParseExpression();

        SyntaxBlockNode? getter = null;
        SyntaxBlockNode? setter = null;
        if (Match(TokenKind.OpenBrace, TokenKind.Arrow))
        {
            var propertyDefinition = ParsePropertyDefinition();
            getter = propertyDefinition.getter;
            setter = propertyDefinition.setter;
        }
        else
        {
            EatExpected(TokenKind.Semicolon);
        }

        var end = _previous!.Span;
        var node = new SyntaxFieldDeclarationNode(
            identifier,
            type,
            value,
            isMutable: keyword.Kind == TokenKind.Var,
            isPublic,
            isStatic,
            getter,
            setter,
            keyword.Span.Combine(end)
        )
        {
            Attributes = attributes,
        };

        var symbol = new FieldSymbol(node);
        scope.AddSymbol(symbol);
        node.Symbol = symbol;

        return node;
    }

    private (SyntaxBlockNode? getter, SyntaxBlockNode? setter) ParsePropertyDefinition()
    {
        if (AdvanceIf(TokenKind.Arrow))
        {
            var start = _previous!.Span;
            var expression = ParseExpression();
            var statement = new SyntaxStatementNode(expression, hasTrailingSemicolon: false);
            EatExpected(TokenKind.Semicolon);
            var singleExpressionGetter = new SyntaxBlockNode(
                [statement],
                isArrowBlock: true,
                start.Combine(expression.Span)
            );

            return (singleExpressionGetter, null);
        }

        var braceStart = EatExpected(TokenKind.OpenBrace).Span;
        SyntaxBlockNode? getterBlock = null;
        if (_current is { Kind: TokenKind.Identifier, Value: "get" })
        {
            Eat();
            getterBlock = ParseBlockOrArrow();

            if (_previous!.Kind != TokenKind.ClosedBrace)
                EatExpected(TokenKind.Semicolon);
        }

        SyntaxBlockNode? setterBlock = null;
        if (_current is { Kind: TokenKind.Identifier, Value: "set" })
        {
            Eat();
            setterBlock = ParseBlockOrArrow();

            if (_previous!.Kind != TokenKind.ClosedBrace)
                EatExpected(TokenKind.Semicolon);
        }

        if (getterBlock == null && setterBlock == null)
        {
            var singleExpression = ParseExpression();
            var statement = new SyntaxStatementNode(singleExpression, hasTrailingSemicolon: false);
            var span = braceStart.Combine(_current?.Span ?? _previous!.Span);
            getterBlock = new SyntaxBlockNode([statement], isArrowBlock: false, span);
        }

        EatExpected(TokenKind.ClosedBrace);

        return (getterBlock, setterBlock);
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

    private SyntaxNode ParseAssignment()
    {
        var left = ParseOr();
        while (AdvanceIf(TokenKind.Equals))
        {
            var right = ParseOr();
            left = new SyntaxAssignmentNode(left, right);
        }

        return left;
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
                TokenKind.LessEquals,
                TokenKind.EqualsEqualsEquals,
                TokenKind.NotEqualsEquals
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
            var value = ParseMemberAccess();

            return new SyntaxUnaryNode(op.Kind, value, op.Span.Combine(value.Span));
        }

        return ParseMemberAccess();
    }

    private SyntaxNode ParseMemberAccess()
    {
        var left = ParseCall();
        while (AdvanceIf(TokenKind.Dot))
        {
            if (Match(TokenKind.As))
            {
                left = ParseDotKeyword(left, areTypeArguments: true);

                continue;
            }

            var identifier = EatExpected(TokenKind.Identifier);
            left = new SyntaxMemberAccessNode(left, identifier);

            if (Match(TokenKind.OpenParenthesis))
            {
                var arguments = ParseArguments();

                return new SyntaxCallNode(left, arguments, left.Span.Combine(_previous!.Span));
            }
        }

        return left;
    }

    private SyntaxDotKeywordNode ParseDotKeyword(SyntaxNode left, bool areTypeArguments)
    {
        var keyword = Eat();
        List<SyntaxNode>? arguments = null;
        if (Match(TokenKind.OpenParenthesis))
        {
            arguments = areTypeArguments
                ? ParseArgumentsAsTypes()
                : ParseArguments();
        }

        return new SyntaxDotKeywordNode(left, keyword, arguments, left.Span.Combine(_previous!.Span));
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

        if (Match(TokenKind.OpenParenthesis))
            return ParseParenthesis();

        if (Match(TokenKind.NumberLiteral, TokenKind.StringLiteral, TokenKind.True, TokenKind.False))
            return ParseLiteral();

        if (Match(TokenKind.Identifier) && _current?.Value == "size_of")
            return ParseKeywordValue(areTypeArguments: true);

        if (Match(TokenKind.Identifier) && _current?.Value == "get_compiler_constant")
            return ParseKeywordValue(areTypeArguments: false);

        if (Match(TokenKind.Self, TokenKind.Base, TokenKind.Default))
            return ParseKeywordValue(areTypeArguments: false);

        if (Match(TokenKind.If))
            return ParseIf();

        if (Match(TokenKind.Identifier))
            return ParseIdentifier();

        if (Match(TokenKind.New))
            return ParseNew();

        if (Match(TokenKind.Return))
            return ParseReturn();

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

        return new SyntaxBlockNode(
            expressions,
            isArrowBlock: false,
            start.Combine(end)
        );
    }

    private SyntaxBlockNode ParseBlockOrArrow()
    {
        if (AdvanceIf(TokenKind.Arrow))
        {
            var start = _previous!.Span;
            SyntaxNode expression;
            try
            {
                expression = ParseStatement(isArrowBlock: true);
            }
            catch (ParserRecoveryException recovery)
            {
                expression = recovery.ErrorNode;
            }

            return new SyntaxBlockNode(
                [expression],
                isArrowBlock: true,
                start.Combine(expression.Span)
            );
        }

        return ParseBlock();
    }

    private SyntaxGroupNode ParseParenthesis()
    {
        var start = EatExpected(TokenKind.OpenParenthesis).Span;
        var value = ParseExpression();
        var end = EatExpected(TokenKind.ClosedParenthesis).Span;

        return new SyntaxGroupNode(value, start.Combine(end));
    }

    private SyntaxLiteralNode ParseLiteral()
    {
        var token = EatExpected(TokenKind.True, TokenKind.False, TokenKind.NumberLiteral, TokenKind.StringLiteral);

        return new SyntaxLiteralNode(token);
    }

    private SyntaxIfNode ParseIf()
    {
        var start = EatExpected(TokenKind.If).Span;
        var condition = ParseExpression();
        var thenBranch = ParseBlockOrArrow();

        SyntaxBlockNode? elseBranch = null;
        if (AdvanceIf(TokenKind.Else))
        {
            var innerStatement = ParseStatement();
            if (innerStatement.Expression is SyntaxBlockNode elseBlock)
            {
                elseBranch = elseBlock;
            }
            else
            {
                elseBranch = new SyntaxBlockNode(
                    [innerStatement],
                    isArrowBlock: false,
                    innerStatement.Span
                );
            }
        }

        return new SyntaxIfNode(
            condition,
            thenBranch,
            elseBranch,
            start.Combine(_previous!.Span)
        );
    }

    private SyntaxIdentifierNode ParseIdentifier()
    {
        var identifierList = new List<Token>()
        {
            EatExpected(TokenKind.Identifier),
        };

        while (AdvanceIf(TokenKind.Colon))
            identifierList.Add(EatExpected(TokenKind.Identifier));

        return new SyntaxIdentifierNode(identifierList);
    }

    private SyntaxNewNode ParseNew()
    {
        var start = EatExpected(TokenKind.New).Span;
        var type = ParseType();
        var arguments = ParseArguments();

        return new SyntaxNewNode(type, arguments, start.Combine(_previous!.Span));
    }

    private SyntaxReturnNode ParseReturn()
    {
        var start = EatExpected(TokenKind.Return).Span;
        var value = Match(TokenKind.Semicolon)
            ? null
            : ParseExpression();

        return new SyntaxReturnNode(value, start.Combine(value?.Span ?? start));
    }

    private SyntaxKeywordValueNode ParseKeywordValue(bool areTypeArguments)
    {
        var keyword = Eat();
        List<SyntaxNode>? arguments = null;
        if (Match(TokenKind.OpenParenthesis))
        {
            arguments = areTypeArguments
                ? ParseArgumentsAsTypes()
                : ParseArguments();
        }

        return new SyntaxKeywordValueNode(keyword, arguments, keyword.Span.Combine(_previous!.Span));
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

    private List<SyntaxNode> ParseArgumentsAsTypes()
    {
        EatExpected(TokenKind.OpenParenthesis);

        var arguments = new List<SyntaxNode>();
        while (!_reachedEnd && !Match(TokenKind.ClosedParenthesis))
        {
            arguments.Add(ParseType());
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
