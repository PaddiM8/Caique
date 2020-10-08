using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class GroupExpression : Expression
    {
        public Expression Expression { get; }

        public GroupExpression(Expression expression, TextSpan span)
            : base(span)
        {
            Expression = expression;
        }

        public override T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}