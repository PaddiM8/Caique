using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedAssignmentStatement : CheckedStatement
    {
        public CheckedExpression Assignee { get; }

        public CheckedExpression Value { get; }

        public CheckedAssignmentStatement(CheckedExpression left,
                                          CheckedExpression value)
        {
            Assignee = left;
            Value = value;
        }

        public override CheckedStatement Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedAssignmentStatement(
                Assignee.Clone(cloningInfo),
                Value.Clone(cloningInfo)
            );
        }
    }
}