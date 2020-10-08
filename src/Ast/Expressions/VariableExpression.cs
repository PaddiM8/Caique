using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class VariableExpression : Expression
    {
        public Token Identifier { get; }

        public VariableExpression(Token identifier)
            : base(identifier.Span)
        {
            Identifier = identifier;
        }

        public override T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}