using System;

namespace Caique.AST
{
    public interface IStatement
    {
        T Accept<T>(IStatementVisitor<T> visitor);
    }
}