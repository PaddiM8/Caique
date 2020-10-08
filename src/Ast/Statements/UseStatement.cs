using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class UseStatement : IStatement
    {
        public List<Token> ModulePath { get; }

        public TextSpan Span { get; }

        public UseStatement(List<Token> modulePath, TextSpan span)
        {
            ModulePath = modulePath;
            Span = span;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}