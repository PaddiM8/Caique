using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class AssignmentStatement : IStatement
    {
        public VariableExpression Variable { get; }

        public Token Operator { get; }

        public IExpression Value { get; }

        public TextSpan Span { get; }

        public AssignmentStatement(VariableExpression identifier, Token op,
                                   IExpression value)
        {
            Variable = identifier;
            Operator = op;
            Value = value;
            Span = Variable.Span.Add(Value.Span);
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}