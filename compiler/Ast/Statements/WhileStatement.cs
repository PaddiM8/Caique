using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class WhileStatement : Statement
    {
        public Expression Condition { get; }

        public BlockExpression Body { get; }

        public WhileStatement(Expression condition, BlockExpression body)
            : base(condition.Span.Add(body.Span))
        {
            Condition = condition;
            Body = body;
        }
    }
}