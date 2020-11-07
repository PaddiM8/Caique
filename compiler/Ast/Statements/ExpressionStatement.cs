using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public class ExpressionStatement : Statement
    {
        public Expression Expression { get; }

        public bool TrailingSemicolon { get; }

        public ExpressionStatement(Expression expr, bool trailingSemicolon)
            : base(expr.Span)
        {
            Expression = expr;
            TrailingSemicolon = trailingSemicolon;
        }
    }
}