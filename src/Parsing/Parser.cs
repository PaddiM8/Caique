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

        private IStatement ParseStatement()
        {
            if (Match(TokenKind.Let))
            {
                return ParseVariableDecl();
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
            Expect(TokenKind.Let, "variable declaration");
            var identifier = Expect(TokenKind.Identifier);
            TypeExpression? type = null;

            if (Expect(TokenKind.Equals, TokenKind.Colon).Kind == TokenKind.Colon)
            {
                type = ParseType();
                Expect(TokenKind.Equals);
            }

            var value = ParseExpression();
            Expect(TokenKind.Semicolon);

            return new VariableDeclStatement(identifier, value, type);
        }

        private ClassDeclStatement ParseClassDecl()
        {
            Expect(TokenKind.Class, "class declaration");
            var identifier = Expect(TokenKind.Identifier);
            TypeExpression? ancestor = null;

            if (Consume(TokenKind.Colon))
                ancestor = ParseType();

            var statement = new ClassDeclStatement(
                identifier,
                ParseBlock(),
                ancestor
            );

            _moduleEnvironment.Parent!.Add(statement);

            return statement;
        }

        private FunctionDeclStatement ParseFunctionDecl()
        {
            Expect(TokenKind.Fn, "function declaration");
            var identifier = Expect(TokenKind.Identifier);
            var parameters = ParseParameters();
            TypeExpression? returnType = Consume(TokenKind.Colon)
                ? ParseType()
                : null;
            var body = ParseBlock();

            var statement = new FunctionDeclStatement(
                identifier,
                parameters,
                body,
                returnType
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
                var expression = ParseExpression();
                Expect(TokenKind.ClosedParenthesis);

                return new GroupExpression(expression);
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
            Expect(TokenKind.If);
            var condition = ParseExpression();
            Consume(TokenKind.Colon); // Consume colon if there is one
            var branch = ParseStatement();
            IStatement? elseBranch = null;

            if (Consume(TokenKind.Else))
                elseBranch = ParseStatement();

            return new IfExpression(condition, branch, elseBranch);
        }

        private BlockExpression ParseBlock()
        {
            Expect(TokenKind.OpenBrace, "block");
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

            var statement = new BlockExpression(statements, _symbolEnvironment);
            _symbolEnvironment = _symbolEnvironment.Parent!; // Return to the parent scope

            return statement;
        }

        private NewExpression ParseNew()
        {
            Expect(TokenKind.New);

            return new NewExpression(ParseModulePath(), ParseArguments());
        }

        private IExpression ParseIdentifier()
        {
            var lookahead = Peek(1)!.Kind;
            if (lookahead == TokenKind.Arrow ||
                lookahead == TokenKind.OpenParenthesis)
            {
                return new CallExpression(ParseModulePath(), ParseArguments());
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

        private List<IExpression> ParseArguments()
        {
            Expect(TokenKind.OpenParenthesis);

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

            Expect(TokenKind.ClosedParenthesis);

            return arguments;
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