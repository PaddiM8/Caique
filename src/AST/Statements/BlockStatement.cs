using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class BlockStatement : IStatement
    {
        public List<IStatement> Statements { get; }

        public TypeExpression? ReturnType { get; set; }

        public BlockStatement(List<IStatement> statements)
        {
            Statements = statements;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}