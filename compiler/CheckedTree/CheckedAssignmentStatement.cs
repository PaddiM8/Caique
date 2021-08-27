using System;
using System.Collections.Generic;
using Caique.Parsing;

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
    }
}