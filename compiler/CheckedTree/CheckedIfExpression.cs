using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedIfExpression : CheckedExpression
    {
        public CheckedExpression Condition { get; }

        public CheckedBlockExpression Branch { get; }

        public CheckedBlockExpression? ElseBranch { get; }

        public CheckedIfExpression(CheckedExpression condition,
                                   CheckedBlockExpression branch,
                                   CheckedBlockExpression? elseBranch) : base(branch.DataType)
        {
            Condition = condition;
            Branch = branch;
            ElseBranch = elseBranch;
        }
    }
}