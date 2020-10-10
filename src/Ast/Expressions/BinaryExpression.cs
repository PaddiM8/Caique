using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class BinaryExpression : Expression
    {
        public Expression Left { get; }

        public Token Operator { get; }

        public Expression Right { get; }

        public BinaryExpression(Expression left, Token op, Expression right)
            : base(left.Span.Add(right.Span))
        {
            Left = left;
            Operator = op;
            Right = right;
        }
    }
}