
using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class KeywordValueExpression : Expression
    {
        public Token Token { get; }

        public List<Expression>? Arguments { get; }

        public KeywordValueExpression(Token token, List<Expression>? arguments = null)
            : base(token.Span)
        {
            Token = token;
            Arguments = arguments;
        }
    }
}