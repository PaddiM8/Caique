using System;
using System.Collections.Generic;
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

        public Parser(List<Token> tokens, DiagnosticBag diagnostics,
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
        public List<Statement> Parse()
        {
            var statements = new List<Statement>();

            // Parse "use" statements at the top first
            while (!IsAtEnd && Match(TokenKind.Use))
            {
                try
                {
                    statements.Add(ParseUse());
                }
                catch (ParsingErrorException)
                {
                    continue;
                }
            }

            while (!IsAtEnd)
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
            else if (Match(TokenKind.Fn))
            {
                return ParseFunctionDecl();
            }
            else if (Match(TokenKind.Class))
            {
                return ParseClassDecl();
            }

            var expressionStatement = ParseExpressionStatement();

            // Assignment statement parsing
            if (Current.Kind.IsAssignmentOperator())
            {
                if (expressionStatement.Expression is VariableExpression variableExpression)
                {
                    return ParseAssignment(variableExpression);
                }
                else
                {
                    _diagnostics.ReportMisplacedAssignmentOperator(Current);
                    throw new ParsingErrorException();
                }
            }

            return expressionStatement;
        }

        private AssignmentStatement ParseAssignment(VariableExpression variableExpression)
        {
            var op = Advance();
            var value = ParseExpression();
            Expect(TokenKind.Semicolon);

            var binaryOpKind = op.Kind switch
            {
                TokenKind.PlusEquals => TokenKind.Plus,
                TokenKind.MinusEquals => TokenKind.Minus,
                TokenKind.StarEquals => TokenKind.Star,
                TokenKind.SlashEquals => TokenKind.Slash,
                _ => throw new NotImplementedException()
            };

            // Turn eg. x += 3 into x = x + 3
            if (op.Kind != TokenKind.EqualsEquals)
            {
                value = new BinaryExpression(
                    variableExpression,
                    new Token(binaryOpKind, "", op.Span), // Turn eg. += into +
                    value
                );
            }

            return new AssignmentStatement(variableExpression, op, value);
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
                type
            );
        }

        private ReturnStatement ParseReturn()
        {
            var start = Expect(TokenKind.Ret).Span;
            var expression = ParseExpressionStatement().Expression;

            return new ReturnStatement(expression, start.Add(expression.Span));
        }

        private FunctionDeclStatement ParseFunctionDecl()
        {
            var start = Expect(TokenKind.Fn, "function declaration").Span;
            var identifier = Expect(TokenKind.Identifier);
            var parameters = ParseParameters();
            TypeExpression? returnType = Consume(TokenKind.Colon)
                ? ParseType()
                : null;
            var body = ParseBlock();

            // Add the parameters to the symbol table
            foreach (var parameter in parameters)
            {
                body.Environment.Add(
                    new VariableDeclStatement(
                        parameter.Identifier,
                        parameter.Type.Span.Add(parameter.Identifier.Span),
                        null,
                        parameter.Type
                    )
                );
            }

            var statement = new FunctionDeclStatement(
                identifier,
                parameters,
                body,
                returnType,
                start.Add(body.Span)
            );

            _symbolEnvironment.Add(statement);

            return statement;
        }

        private ClassDeclStatement ParseClassDecl()
        {
            Expect(TokenKind.Class, "class declaration");
            var identifier = Expect(TokenKind.Identifier);

            // Parameter refs
            var parameterRefs = new List<Token>();
            Expect(TokenKind.OpenParenthesis);

            if (Match(TokenKind.Identifier))
            {
                do parameterRefs.Add(Expect(TokenKind.Identifier));
                while (Consume(TokenKind.Comma));
            }

            Expect(TokenKind.ClosedParenthesis);

            // Inheritance
            TypeExpression? ancestor = null;
            if (Consume(TokenKind.Colon))
                ancestor = ParseType();

            var statement = new ClassDeclStatement(
                identifier,
                parameterRefs,
                ParseClassBlock(),
                identifier.Span,
                _moduleEnvironment,
                ancestor
            );

            _moduleEnvironment.Parent!.Add(statement);

            return statement;
        }

        private BlockExpression ParseClassBlock()
        {
            var start = Expect(TokenKind.OpenBrace).Span;
            var statements = new List<Statement>();
            _symbolEnvironment = _symbolEnvironment.CreateChildEnvironment();
            int variableDeclIndex = 0;
            while (!IsAtEnd && !Consume(TokenKind.ClosedBrace))
            {
                if (Match(TokenKind.Fn))
                {
                    statements.Add(ParseFunctionDecl());
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

            return statement;
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
                type,
                VariableType.Object,
                index
            );

            _symbolEnvironment.Add(statement);

            return statement;
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
            var left = ParsePrimary();

            if (Consume(TokenKind.Dot))
            {
                return new DotExpression(left, ParseDot());
            }

            return left;
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
            else if (Match(TokenKind.NumberLiteral, TokenKind.StringLiteral))
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
                branch,
                elseBranch,
                start.Add(end)
            );
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

        private List<Parameter> ParseParameters()
        {
            Expect(TokenKind.OpenParenthesis);

            var parameters = new List<Parameter>();
            do
            {
                // In case of trailing comma,
                // a closed parenthesis would come right after the comma,
                // which should be allowed.
                if (Match(TokenKind.ClosedParenthesis)) break;

                var identifier = Expect(TokenKind.Identifier, "parameter name");
                Expect(TokenKind.Colon);
                var type = ParseType();
                parameters.Add(new Parameter(identifier, type));
            }
            while (!IsAtEnd && Consume(TokenKind.Comma));

            Expect(TokenKind.ClosedParenthesis);

            return parameters;
        }

        private TypeExpression ParseType()
        {
            if (Match(TokenKind.Identifier))
            {
                return new TypeExpression(ParseModulePath());
            }
            else
            {
                return new TypeExpression(new List<Token>()
                {
                    Advance()
                });
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
            throw new ParsingErrorException();
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
            throw new ParsingErrorException();
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