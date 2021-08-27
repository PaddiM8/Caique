using System;
using Caique.Parsing;

namespace Caique.CheckedTree
{
    public class CheckedWhileStatement : CheckedStatement
    {
        public CheckedExpression Condition { get; }

        public CheckedBlockExpression Body { get; }

        public CheckedWhileStatement(CheckedExpression condition, CheckedBlockExpression body)
        {
            Condition = condition;
            Body = body;
        }
    }
}