using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class IfExpression : Expression
    {
        public Expression Condition { get; }

        public ExpressionStatement Branch { get; }

        public ExpressionStatement? ElseBranch { get; }

        public IfExpression(Expression condition, ExpressionStatement branch,
                            ExpressionStatement? elseBranch, TextSpan span) : base(span)
        {
            Condition = condition;
            Branch = branch;
            ElseBranch = elseBranch;
        }
    }
}