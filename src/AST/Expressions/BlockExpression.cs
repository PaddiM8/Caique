using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.AST
{
    public class BlockExpression : IExpression
    {
        public List<IStatement> Statements { get; }

        public SymbolEnvironment Environment { get; }

        public BlockExpression(List<IStatement> statements, SymbolEnvironment environment)
        {
            Statements = statements;
            Environment = environment;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}