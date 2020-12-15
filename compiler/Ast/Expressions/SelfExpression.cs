
using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class SelfExpression : Expression
    {
        public SelfExpression(Token token) : base(token.Span)
        {
        }
    }
}