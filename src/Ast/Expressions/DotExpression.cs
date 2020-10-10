using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class DotExpression : Expression
    {
        public Expression Left { get; }

        public Expression Right { get; }

        public DotExpression(Expression left, Expression right)
            : base(left.Span.Add(right.Span))
        {
            Left = left;
            Right = right;
        }
    }
}