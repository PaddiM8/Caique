using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class IfExpression : Expression
    {
        public Expression Condition { get; }

        public BlockExpression Branch { get; }

        public BlockExpression? ElseBranch { get; }

        public IfExpression(Expression condition, BlockExpression branch,
                            BlockExpression? elseBranch, TextSpan span) : base(span)
        {
            Condition = condition;
            Branch = branch;
            ElseBranch = elseBranch;
        }
    }
}