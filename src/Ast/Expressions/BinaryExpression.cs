using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class BinaryExpression : IExpression
    {
        public IExpression Left { get; }

        public Token Operator { get; }

        public IExpression Right { get; }

        public TextSpan Span { get; }

        public BinaryExpression(IExpression left, Token op, IExpression right)
        {
            Left = left;
            Operator = op;
            Right = right;
            Span = left.Span.Add(right.Span);
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}