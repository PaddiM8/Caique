using System;
using System.Collections.Generic;
using Caique.AST;
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
        private ModuleEnvironment _moduleEnvironment;
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

        public List<IStatement> Parse()
        {
            var statements = new List<IStatement>();

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

        private IStatement ParseStatement()
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

            return ParseExpressionStatement();
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

            var value = ParseExpression();
            var end = Expect(TokenKind.Semicolon).Span;

            return new VariableDeclStatement(
                identifier,
                start.Add(end),
                value,
                type
            );
        }

        private ReturnStatement ParseReturn()
        {
            var start = Expect(TokenKind.Ret).Span;
            var expression = ParseExpression();
            var end = Expect(TokenKind.Semicolon).Span;

            return new ReturnStatement(expression, start.Add(end));
        }

        private ClassDeclStatement ParseClassDecl()
        {
            var start = Expect(TokenKind.Class, "class declaration").Span;
            var identifier = Expect(TokenKind.Identifier);
            TypeExpression? ancestor = null;

            if (Consume(TokenKind.Colon))
                ancestor = ParseType();

            var block = ParseBlock();
            var statement = new ClassDeclStatement(
                identifier,
                block,
                start.Add(block.Span),
                ancestor
            );

            _moduleEnvironment.Parent!.Add(statement);

            return statement;
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

        private IStatement ParseExpressionStatement()
        {
            var expression = ParseExpression();

            // Assignment statement parsing
            if (Current.Kind.IsAssignmentOperator())
            {
                if (expression is VariableExpression variableExpression)
                {
                    var op = Advance();
                    var value = ParseExpression();
                    Expect(TokenKind.Semicolon);

                    return new AssignmentStatement(variableExpression, op, value);
                }
                else
                {
                    _diagnostics.ReportMisplacedAssignmentOperator(Current);
                    throw new ParsingErrorException();
                }
            }

            return new ExpressionStatement(
                expression,
                Consume(TokenKind.Semicolon)
            );
        }

        private IExpression ParseExpression()
        {
            return ParseBinary();
        }

        private IExpression ParseBinary(int parentPrecendece = 0)
        {
            IExpression left;

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

        private IExpression ParseDot()
        {
            var left = ParsePrimary();

            if (Consume(TokenKind.Dot))
            {
                return new DotExpression(left, ParseDot());
            }

            return left;
        }

        private IExpression ParsePrimary()
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
            IStatement? elseBranch = null;

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
            var statements = new List<IStatement>();
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
            var modulePath = ParseModulePath();
            var (arguments, argumentsSpan) = ParseArguments();

            return new NewExpression(
                modulePath,
                arguments,
                start.Add(argumentsSpan)
            );
        }

        private IExpression ParseIdentifier()
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

        private (List<IExpression> arguments, TextSpan span) ParseArguments()
        {
            var start = Expect(TokenKind.OpenParenthesis).Span;

            var arguments = new List<IExpression>();
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
            return new TypeExpression(Advance());
        }

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

        private bool Match(params TokenKind[] kinds)
        {
            foreach (var kind in kinds)
            {
                if (Current.Kind == kind) return true;
            }

            return false;
        }

        private Token? Peek(int amount)
        {
            if (_index + amount >= _tokens.Count) return null;

            return _tokens[_index + amount];
        }

        private Token Expect(TokenKind kind, string description)
        {
            if (Current.Kind == kind) return Advance();

            _diagnostics.ReportUnexpectedToken(Current, description);
            throw new ParsingErrorException();
        }

        private Token Expect(params TokenKind[] kinds)
        {
            foreach (var kind in kinds)
            {
                if (Current.Kind == kind) return Advance();
            }

            _diagnostics.ReportUnexpectedToken(Current, kinds);
            throw new ParsingErrorException();
        }

        private bool Consume(TokenKind kind)
        {
            if (Current.Kind == kind)
            {
                Advance();

                return true;
            }

            return false;
        }

        private Token Advance()
        {
            var token = Current;
            _index++;

            return token;
        }
    }
}