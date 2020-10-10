using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class AssignmentStatement : Statement
    {
        public VariableExpression Variable { get; }

        public Token Operator { get; }

        public Expression Value { get; }

        public AssignmentStatement(VariableExpression identifier, Token op,
                                   Expression value)
                                   : base(identifier.Span.Add(value.Span))
        {
            Variable = identifier;
            Operator = op;
            Value = value;
        }
    }
}