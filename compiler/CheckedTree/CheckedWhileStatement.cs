using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

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

        public override CheckedStatement Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedWhileStatement(
                Condition.Clone(cloningInfo),
                (CheckedBlockExpression)Body.Clone(cloningInfo)
            );
        }
    }
}