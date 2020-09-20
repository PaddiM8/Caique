using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class DotExpression : IExpression
    {
        public IExpression Left { get; }

        public IExpression Right { get; }

        public DotExpression(IExpression left, IExpression right)
        {
            Left = left;
            Right = right;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}