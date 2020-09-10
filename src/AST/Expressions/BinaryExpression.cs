using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class BinaryExpression : IExpression
    {
        public IExpression Left { get; }

        public Token Operator { get; }

        public IExpression Right { get; }

        public BinaryExpression(IExpression left, Token op, IExpression right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}