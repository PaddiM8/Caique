
using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class KeywordValueExpression : Expression
    {
        public Token Token { get; }

        public KeywordValueExpression(Token token) : base(token.Span)
        {
            Token = token;
        }
    }
}