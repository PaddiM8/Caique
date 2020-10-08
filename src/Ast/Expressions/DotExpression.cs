using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class DotExpression : IExpression
    {
        public IExpression Left { get; }

        public IExpression Right { get; }

        public TextSpan Span { get; }

        public DotExpression(IExpression left, IExpression right)
        {
            Left = left;
            Right = right;
            Span = Left.Span.Add(Right.Span);
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}