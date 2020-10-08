using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public interface IStatement
    {
        TextSpan Span { get; }

        T Accept<T>(IStatementVisitor<T> visitor);
    }
}