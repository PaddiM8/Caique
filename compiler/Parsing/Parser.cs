using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Ast;
using Caique.Diagnostics;
using Caique.Semantics;

namespace Caique.Parsing
{
    class Parser
    {
        private bool IsAtEnd
        {
            get
            {
                return Current.Kind == TokenKind.EndOfFile ||
                       _index >= _tokens.Count;
            }
        }

        private Token Current
        {
            get
            {
                return _tokens[_index];
            }
        }

        private Token Previous
        {
            get
            {
                return _tokens[_index - 1];
            }
        }

        private readonly List<Token> _tokens;
        private readonly DiagnosticBag _diagnostics;
        private readonly ModuleEnvironment _moduleEnvironment;
        private SymbolEnvironment _symbolEnvironment;
        private int _index;
        private static readonly TypeExpression _objectTypeExpression = new(new List<Token>
            {
                new Token(
                    TokenKind.Identifier,
                    "object",
                    new TextSpan(new TextPosition(0, 0), new TextPosition(0, 0))
                )
            }, null);

        public Parser(List<Token> tokens,
                      DiagnosticBag diagnostics,
                      ModuleEnvironment moduleEnvironment)
        {
            _tokens = tokens;
            _diagnostics = diagnostics;
            _moduleEnvironment = moduleEnvironment;
            _symbolEnvironment = moduleEnvironment.SymbolEnvironment;
        }

        /// <summary>
        /// Start the parsing process.
        /// </summary>
        /// <returns>List of abstract syntax trees.</returns>
        public static List<Statement> Parse(List<Token> tokens,
                                            DiagnosticBag diagnostics,
                                            ModuleEnvironment moduleEnvironment)
        {
            var parser = new Parser(tokens, diagnostics, moduleEnvironment);
            var statements = new List<Statement>();

            // Parse "use" statements at the top first
            while (!parser.IsAtEnd && parser.Match(TokenKind.Use))
            {
                try
                {
                    statements.Add(parser.ParseUse());
                }
                catch (ParsingErrorException)
                {
                    continue;
                }
            }

            while (!parser.IsAtEnd)
            {
                try
                {
                    statements.Add(parser.ParseStatement());
                }
                catch (ParsingErrorException)
                {
                    parser.Synchronise();
                }
            }

            return statements;
        }

        private UseStatement ParseUse()
        {
            var start = Expect(TokenKind.Use).Span;
            var modulePath = ParseModulePath();
            var end = Expect(TokenKind.Semicolon).Span;
            var statement = new UseStatement(modulePath, start.Add(end));

            return statement;
        }

        private Statement ParseStatement()
        {
            if (Match(TokenKind.Let))
            {
                return ParseVariableDecl();
            }
            if (Match(TokenKind.Ret))
            {
                return ParseReturn();
            }
            else if (Match(TokenKind.Fn, TokenKind.Ext))
            {
                return ParseFunctionDecl();
            }
            else if (Match(TokenKind.Class))
            {
                return ParseClassDecl();
            }
            else if (Match(TokenKind.While))
            {
                return ParseWhile();
            }

            var expressionStatement = ParseExpressionStatement();

            // Assignment statement parsing
            if (Current.Kind.IsAssignmentOperator())
            {
                return ParseAssignment(expressionStatement.Expression);
            }

            return expressionStatement;
        }

        private AssignmentStatement ParseAssignment(Expression expression)
        {
            var op = Advance();
            var value = ParseExpression();
            Expect(TokenKind.Semicolon);

            TokenKind? modifyingOpKind = op.Kind switch
            {
                TokenKind.PlusEquals => TokenKind.Plus,
                TokenKind.MinusEquals => TokenKind.Minus,
                TokenKind.StarEquals => TokenKind.Star,
                TokenKind.SlashEquals => TokenKind.Slash,
                _ => null,
            };

            // Turn eg. x += 3 into x = x + 3
            if (modifyingOpKind != null)
            {
                value = new BinaryExpression(
                    expression,
                    new Token(modifyingOpKind!.Value, "", op.Span), // Turn eg. += into +
                    value
                );
            }

            if (expression is DotExpression dotExpression)
            {
                return new AssignmentStatement(dotExpression, value);
            }
            else
            {
                return new AssignmentStatement(new DotExpression(new() { expression }), value);
            }

        }

        private VariableDeclStatement ParseVariableDecl()
        {
            var start = Expect(TokenKind.Let, "variable declaration").Span;
            var identifier = Expect(TokenKind.Identifier);
            TypeExpression? type = null;

            if (Expect(TokenKind.Equals, TokenKind.Colon).Kind == TokenKind.Colon)
            {
                type = ParseType();
                Expect(TokenKind.Equals);
            }

            var value = ParseExpressionStatement().Expression;

            return new VariableDeclStatement(
                identifier,
                start.Add(value.Span),
                value,
                VariableType.Local,
                type
            );
        }

        private ReturnStatement ParseReturn()
        {
            var start = Expect(TokenKind.Ret).Span;
            var expression = Consume(TokenKind.Semicolon)
                ? null
                : ParseExpressionStatement().Expression;

            return new ReturnStatement(expression, start);
        }

        private FunctionDeclStatement ParseFunctionDecl()
        {
            TypeExpression? extensionOf = null;
            TextSpan? start = null;
            if (Consume(TokenKind.Fn))
            {
                start = Previous.Span;
            }
            else if (Consume(TokenKind.Ext))
            {
                start = Previous.Span;
                extensionOf = ParseType();
            }

            var identifier = Expect(TokenKind.Identifier);
            var parameters = ParseParameters();
            TypeExpression? returnType = Consume(TokenKind.Colon)
                ? ParseType()
                : null;
            BlockExpression? body = null;

            // If it has a body
            if (!Consume(TokenKind.Semicolon))
            {
                body = ParseBlock();

                // Add the parameters to the symbol table
                foreach (var parameter in parameters)
                {
                    body.Environment.TryAdd(parameter);
                }
            }

            var statement = new FunctionDeclStatement(
                identifier,
                parameters,
                body,
                returnType,
                false,
                start!.Add(start)
            )
            {
                ExtensionOf = extensionOf
            };

            _symbolEnvironment.Add(statement);

            return statement;
        }

        private ClassDeclStatement ParseClassDecl()
        {
            Expect(TokenKind.Class, "class declaration");
            var identifier = Expect(TokenKind.Identifier);

            // Generics
            List<Token>? typeParameters = null;
            if (Match(TokenKind.OpenSquareBracket))
            {
                typeParameters = ParseTypeParameters();
            }

            // Inheritance
            TypeExpression? ancestor;
            if (Consume(TokenKind.Colon)) ancestor = ParseType();
            else ancestor = identifier.Value == "object" ? null : _objectTypeExpression;

            var (block, init) = ParseClassBlock();
            var statement = new ClassDeclStatement(
                identifier,
                typeParameters,
                block,
                identifier.Span,
                _moduleEnvironment,
                _symbolEnvironment,
                ancestor,
                init
            );

            _symbolEnvironment.Add(statement);

            return statement;
        }

        private (BlockExpression block, FunctionDeclStatement? initFunction) ParseClassBlock()
        {
            var start = Expect(TokenKind.OpenBrace).Span;
            var statements = new List<Statement>();
            _symbolEnvironment = _symbolEnvironment.CreateChildEnvironment();
            int variableDeclIndex = 0;
            FunctionDeclStatement? initFunction = null;

            while (!IsAtEnd && !Consume(TokenKind.ClosedBrace))
            {
                if (Match(TokenKind.Fn))
                {
                    statements.Add(ParseFunctionDecl());
                }
                else if (Match(TokenKind.Init))
                {
                    if (initFunction != null)
                    {
                        _diagnostics.ReportCanOnlyHaveOneConstructor(Current);
                        continue;
                    }

                    initFunction = ParseInit();
                }
                else
                {
                    statements.Add(ParseObjectVariableDecl(variableDeclIndex++));
                }
            }

            var statement = new BlockExpression(
                statements,
                _symbolEnvironment,
                start.Add(Previous.Span)
            );

            _symbolEnvironment = _symbolEnvironment.Parent!;

            return (statement, initFunction);
        }

        private VariableDeclStatement ParseObjectVariableDecl(int index)
        {
            var identifier = Expect(TokenKind.Identifier);
            Expect(TokenKind.Colon);
            var type = ParseType();
            Expression? value = null;

            if (Consume(TokenKind.Equals))
            {
                value = ParseExpression();
            }

            Expect(TokenKind.Semicolon);

            var statement = new VariableDeclStatement(
                identifier,
                identifier.Span.Add(type.Span),
                value,
                VariableType.Object,
                type,
                index
            );

            _symbolEnvironment.TryAdd(statement);

            return statement;
        }

        private WhileStatement ParseWhile()
        {
            Expect(TokenKind.While);
            var condition = ParseExpression();
            Consume(TokenKind.Colon); // Consume a colon if there is one.
            var body = ParseStatement();

            return new WhileStatement(condition, AddBlockToBranchIfNeeded(body));
        }

        private FunctionDeclStatement ParseInit()
        {
            var keyword = Expect(TokenKind.Init);

            // Parameters
            Expect(TokenKind.OpenParenthesis);
            var parameters = new List<VariableDeclStatement>();
            do
            {
                var identifier = Expect(TokenKind.Identifier);
                TypeExpression? type = null;

                if (Consume(TokenKind.Colon))
                    type = ParseType();

                parameters.Add(new VariableDeclStatement(
                    identifier,
                    identifier.Span,
                    null,
                    VariableType.FunctionParameter,
                    type
                ));
            }
            while (Consume(TokenKind.Comma));

            Expect(TokenKind.ClosedParenthesis);

            var block = ParseBlock();

            // Add parameters to symbol table
            foreach (var parameter in parameters)
            {
                if (parameter.SpecifiedType == null) continue;
                block.Environment.TryAdd(parameter);
            }

            return new FunctionDeclStatement(
                keyword,
                parameters,
                block,
                null,
                true,
                keyword.Span
            );
        }

        private ExpressionStatement ParseExpressionStatement()
        {
            var expression = ParseExpression();

            return new ExpressionStatement(
                expression,
                Consume(TokenKind.Semicolon)
            );
        }

        private Expression ParseExpression()
        {
            return ParseBinary();
        }

        private Expression ParseBinary(int parentPrecendece = 0)
        {
            Expression left;

            int unaryOperatorPrecedence = Current.Kind.GetUnaryOperatorPrecedence();
            if (unaryOperatorPrecedence != 0 && unaryOperatorPrecedence >= parentPrecendece)
            {
                left = new UnaryExpression(
                    Advance(),
                    ParseBinary(unaryOperatorPrecedence)
                );
            }
            else
            {
                left = ParseDot();
            }

            while (true)
            {
                int precedence = Current.Kind.GetBinaryOperatorPrecedence();
                if (precedence == 0 || precedence <= parentPrecendece)
                    break;

                var op = Advance();
                left = new BinaryExpression(left, op, ParseBinary(precedence));
            }

            return left;
        }

        private Expression ParseDot()
        {
            var expressions = new List<Expression>()
            {
                ParsePrimary()
            };


            while (Consume(TokenKind.Dot))
            {
                expressions.Add(ParsePrimary());
            }

            return expressions.Count > 1
                ? new DotExpression(expressions)
                : expressions.First();
        }

        private Expression ParsePrimary()
        {
            if (Match(TokenKind.If))
            {
                return ParseIf();
            }
            else if (Match(TokenKind.OpenParenthesis))
            {
                var start = Expect(TokenKind.OpenParenthesis).Span;
                var expression = ParseExpression();
                var end = Expect(TokenKind.ClosedParenthesis).Span;

                return new GroupExpression(expression, start.Add(end));
            }
            else if (Match(TokenKind.NumberLiteral, TokenKind.StringLiteral, TokenKind.CharLiteral))
            {
                return new LiteralExpression(Advance());
            }
            else if (Match(TokenKind.OpenBrace))
            {
                return ParseBlock();
            }
            else if (Match(TokenKind.New))
            {
                return ParseNew();
            }
            else if (Match(TokenKind.Self, TokenKind.True, TokenKind.False))
            {
                return new KeywordValueExpression(Advance());
            }
            else if (Match(TokenKind.Identifier))
            {
                return ParseIdentifier();
            }

            _diagnostics.ReportUnexpectedToken(Current, "primary token");
            throw new ParsingErrorException();
        }

        private IfExpression ParseIf()
        {
            var start = Expect(TokenKind.If).Span;
            var condition = ParseExpression();
            Consume(TokenKind.Colon); // Consume colon if there is one
            var branch = ParseStatement();
            var end = branch.Span;
            Statement? elseBranch = null;

            if (Consume(TokenKind.Else))
            {
                elseBranch = ParseStatement();
                end = elseBranch.Span;
            }


            return new IfExpression(
                condition,
                AddBlockToBranchIfNeeded(branch),
                elseBranch == null ? null : AddBlockToBranchIfNeeded(elseBranch),
                start.Add(end)
            );
        }

        private BlockExpression AddBlockToBranchIfNeeded(Statement branch)
        {
            if (branch is ExpressionStatement exprStmt)
            {
                if (exprStmt.Expression is BlockExpression blockExpr)
                {
                    return blockExpr;
                }
                else
                {
                    return new BlockExpression(
                        new() { new ExpressionStatement(exprStmt.Expression, false) },
                        _symbolEnvironment.CreateChildEnvironment(),
                        branch.Span
                    );
                }
            }
            else
            {
                return new BlockExpression(
                    new() { branch },
                    _symbolEnvironment.CreateChildEnvironment(),
                    branch.Span
                );
            }
        }

        private BlockExpression ParseBlock()
        {
            var start = Expect(TokenKind.OpenBrace, "block").Span;
            var statements = new List<Statement>();
            _symbolEnvironment = _symbolEnvironment.CreateChildEnvironment(); // Create the scope

            while (!Consume(TokenKind.ClosedBrace))
            {
                try
                {
                    statements.Add(ParseStatement());
                }
                catch (ParsingErrorException)
                {
                    Synchronise();
                }
            }

            var statement = new BlockExpression(
                statements,
                _symbolEnvironment,
                start.Add(Previous.Span)
            );
            _symbolEnvironment = _symbolEnvironment.Parent!; // Return to the parent scope

            return statement;
        }

        private NewExpression ParseNew()
        {
            var start = Expect(TokenKind.New).Span;
            var type = ParseType();
            var (arguments, argumentsSpan) = ParseArguments();

            return new NewExpression(
                type,
                arguments,
                start.Add(argumentsSpan)
            );
        }

        private Expression ParseIdentifier()
        {
            var lookahead = Peek(1)!.Kind;
            if (lookahead == TokenKind.Arrow ||
                lookahead == TokenKind.OpenParenthesis)
            {
                var modulePath = ParseModulePath();
                var (arguments, argumentsSpan) = ParseArguments();

                return new CallExpression(
                    modulePath,
                    arguments,
                    modulePath[0].Span.Add(argumentsSpan)
                );
            }

            return new VariableExpression(Expect(TokenKind.Identifier));
        }

        private List<Token> ParseModulePath()
        {
            var identifiers = new List<Token>()
            {
                Expect(TokenKind.Identifier),
            };

            while (Consume(TokenKind.Arrow))
            {
                identifiers.Add(Expect(TokenKind.Identifier));
            }

            return identifiers;
        }

        private (List<Expression> arguments, TextSpan span) ParseArguments()
        {
            var start = Expect(TokenKind.OpenParenthesis).Span;

            var arguments = new List<Expression>();
            do
            {
                // In case of trailing comma,
                // a closed parenthesis would come right after the comma,
                // which should be allowed.
                if (Match(TokenKind.ClosedParenthesis)) break;

                arguments.Add(ParseExpression());
            }
            while (!IsAtEnd && Consume(TokenKind.Comma));

            var end = Expect(TokenKind.ClosedParenthesis).Span;

            return (arguments, start.Add(end));
        }

        private List<VariableDeclStatement> ParseParameters()
        {
            Expect(TokenKind.OpenParenthesis);

            var parameters = new List<VariableDeclStatement>();
            do
            {
                // In case of trailing comma,
                // a closed parenthesis would come right after the comma,
                // which should be allowed.
                if (Match(TokenKind.ClosedParenthesis)) break;

                var identifier = Expect(TokenKind.Identifier, "parameter name");
                Expect(TokenKind.Colon);
                var type = ParseType();
                parameters.Add(new VariableDeclStatement(
                    identifier,
                    type.Span.Add(identifier.Span),
                    null,
                    VariableType.FunctionParameter,
                    type
                ));
            }
            while (!IsAtEnd && Consume(TokenKind.Comma));

            Expect(TokenKind.ClosedParenthesis);

            return parameters;
        }

        private List<Token> ParseTypeParameters()
        {
            Expect(TokenKind.OpenSquareBracket);

            var parameters = new List<Token>();
            do
            {
                parameters.Add(Expect(TokenKind.Identifier));
            }
            while (!IsAtEnd && Consume(TokenKind.Comma));

            Expect(TokenKind.ClosedSquareBracket);

            return parameters;
        }

        private List<TypeExpression> ParseTypeArguments()
        {
            Expect(TokenKind.OpenSquareBracket);

            var arguments = new List<TypeExpression>();
            do
            {
                arguments.Add(ParseType());
            }
            while (!IsAtEnd && Consume(TokenKind.Comma));

            Expect(TokenKind.ClosedSquareBracket);

            return arguments;
        }

        private TypeExpression ParseType()
        {
            if (Match(TokenKind.Identifier))
            {
                return new TypeExpression(
                    ParseModulePath(),
                    Match(TokenKind.OpenSquareBracket) ? ParseTypeArguments() : null,
                    Consume(TokenKind.Star)
                );
            }
            else
            {
                return new TypeExpression(
                    new() { Advance() },
                    null,
                    Consume(TokenKind.Star)
                );
            }
        }

        /// <summary>
        /// Recover from errors by proceeding to the next
        /// statement or expression.
        /// </summary>
        private void Synchronise()
        {
            Advance();

            while (!IsAtEnd)
            {
                if (Consume(TokenKind.Semicolon)) return;
                if (Current.Kind.IsKeyword() ||
                    Match(TokenKind.ClosedBrace)) return;

                Advance();
            }
        }

        /// <summary>
        /// Whether or not the current token is of a certain type.
        /// </summary>
        /// <param name="kinds">Checks if one if the current token is of the same type as *one* of these.</param>
        private bool Match(params TokenKind[] kinds)
        {
            foreach (var kind in kinds)
            {
                if (Current.Kind == kind) return true;
            }

            return false;
        }

        /// <summary>
        /// Get a token that is somewhere after the current one.
        /// </summary>
        /// <param name="amount">How many items away.</param>
        private Token? Peek(int amount)
        {
            if (_index + amount >= _tokens.Count) return null;

            return _tokens[_index + amount];
        }

        /// <summary>
        /// Consume the current token if it is the right kind.
        /// If it is the wrong kind, create an error.
        /// </summary>
        /// <param name="kind">Token kind to expect.</param>
        /// <param name="description">Description of the expected token.</param>
        /// <returns>The consumed token.</returns>
        private Token Expect(TokenKind kind, string description)
        {
            if (Current.Kind == kind) return Advance();
            _diagnostics.ReportUnexpectedToken(Current, description);

            return new Token(kind, "", Peek(0)!.Span);
        }

        /// <summary>
        /// Consume the current token if it is the right kind.
        /// If it is the wrong kind, create an error.
        /// </summary>
        /// <param name="kind">Token kind to expect.</param>
        /// <returns>The consumed token.</returns>
        private Token Expect(params TokenKind[] kinds)
        {
            foreach (var kind in kinds)
            {
                if (Current.Kind == kind) return Advance();
            }

            _diagnostics.ReportUnexpectedToken(Current, kinds);
            if (kinds.Length == 1)
            {
                return new Token(kinds[0], "", Peek(0)!.Span);
            }
            else
            {
                throw new ParsingErrorException();
            }
        }

        /// <summary>
        /// If the current token is of the following type,
        /// advance and return true. Otherwise just return false.
        /// </summary>
        /// <param name="kind">Token kind to expect.</param>
        /// <returns>Whether or not the token kind was found.</returns>
        private bool Consume(TokenKind kind)
        {
            if (Current.Kind == kind)
            {
                Advance();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Move on to the next token.
        /// </summary>
        /// <returns>The previous token.</returns>
        private Token Advance()
        {
            var token = Current;
            _index++;

            return token;
        }
    }
}