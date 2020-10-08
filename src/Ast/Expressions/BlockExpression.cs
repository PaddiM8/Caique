using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class BlockExpression : IExpression
    {
        public List<IStatement> Statements { get; }

        public SymbolEnvironment Environment { get; }

        public TextSpan Span { get; }

        public BlockExpression(List<IStatement> statements, SymbolEnvironment environment,
                               TextSpan span)
        {
            Statements = statements;
            Environment = environment;
            Span = span;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}