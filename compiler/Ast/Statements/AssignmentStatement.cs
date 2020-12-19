using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class AssignmentStatement : Statement
    {
        public DotExpression Assignee { get; }

        public Expression Value { get; }

        public AssignmentStatement(DotExpression left,
                                   Expression value)
                                   : base(left.Span.Add(value.Span))
        {
            Assignee = left;
            Value = value;
        }
    }
}