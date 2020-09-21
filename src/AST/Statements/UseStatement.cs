using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class UseStatement : IStatement
    {
        public List<Token> ModulePath { get; }

        public UseStatement(List<Token> modulePath)
        {
            ModulePath = modulePath;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}