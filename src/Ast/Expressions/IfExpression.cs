using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class IfExpression : Expression
    {
        public Expression Condition { get; }

        public Statement Branch { get; }

        public Statement? ElseBranch { get; }

        public IfExpression(Expression condition, Statement branch,
                            Statement? elseBranch, TextSpan span) : base(span)
        {
            Condition = condition;
            Branch = branch;
            ElseBranch = elseBranch;
        }
    }
}