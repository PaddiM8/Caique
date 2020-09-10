using System;
using System.Collections.Generic;
using Caique.AST;
using Caique.Diagnostics;

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
        private int _index;

        public Parser(List<Token> tokens, DiagnosticBag diagnostics)
        {
            _tokens = tokens;
            _diagnostics = diagnostics;
        }

        public List<IStatement> Parse()
        {
            var statements = new List<IStatement>();

            while (!IsAtEnd)
            {
                statements.Add(ParseStatement());
            }

            return statements;
        }

        private IStatement ParseStatement()
        {
            return new ExpressionStatement(ParseBinary());
        }

        private IExpression ParseBinary(int parentPrecendece = 0)
        {
            IExpression left = ParsePrimary();

            while (true)
            {
                int precedence = Current.Kind.GetBinaryOperatorPrecedence();
                if (precedence == 0 || precedence <= parentPrecendece)
                    break;

                var op = Advance();
                var right = ParseBinary(precedence);
                left = new BinaryExpression(left, op, right);
            }

            return left;
        }

        private IExpression ParsePrimary()
        {
            return new LiteralExpression(Advance());
        }

        private Token Advance()
        {
            var token = Current;
            _index++;

            return token;
        }
    }
}