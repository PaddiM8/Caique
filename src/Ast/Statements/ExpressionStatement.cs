using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class ExpressionStatement : IStatement
    {
        public IExpression Expression { get; }

        public bool TrailingSemicolon { get; }

        public TextSpan Span { get; }

        public ExpressionStatement(IExpression expr, bool trailingSemicolon)
        {
            Expression = expr;
            TrailingSemicolon = trailingSemicolon;
            Span = Expression.Span;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}