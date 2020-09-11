using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class VariableExpression : IExpression
    {
        public Token Identifier { get; }

        public VariableExpression(Token identifier)
        {
            Identifier = identifier;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}