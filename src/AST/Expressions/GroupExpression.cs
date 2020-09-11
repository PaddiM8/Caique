using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class GroupExpression : IExpression
    {
        public IExpression Expression { get; }

        public GroupExpression(IExpression expression)
        {
            Expression = expression;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}