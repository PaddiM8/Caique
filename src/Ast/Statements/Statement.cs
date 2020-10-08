using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public abstract class Statement
    {
        public TextSpan Span { get; }

        public Statement(TextSpan span)
        {
            Span = span;
        }

        public abstract T Accept<T>(IStatementVisitor<T> visitor);
    }
}