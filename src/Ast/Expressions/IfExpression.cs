using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class IfExpression : IExpression
    {
        public IExpression Condition { get; }

        public IStatement Branch { get; }

        public IStatement? ElseBranch { get; }

        public TextSpan Span { get; }

        public IfExpression(IExpression condition, IStatement branch,
                            IStatement? elseBranch, TextSpan span)
        {
            Condition = condition;
            Branch = branch;
            ElseBranch = elseBranch;
            Span = span;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}