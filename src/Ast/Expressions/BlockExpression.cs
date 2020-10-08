using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class BlockExpression : Expression
    {
        public List<Statement> Statements { get; }

        public SymbolEnvironment Environment { get; }

        public BlockExpression(List<Statement> statements, SymbolEnvironment environment,
                               TextSpan span) : base(span)
        {
            Statements = statements;
            Environment = environment;
        }

        public override T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}