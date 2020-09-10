using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class ExpressionStatement : IStatement
    {
        public IExpression Expression { get; }

        public ExpressionStatement(IExpression expr)
        {
            Expression = expr;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}