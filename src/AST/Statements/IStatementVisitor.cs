using System;

namespace Caique.AST
{
    public interface IStatementVisitor<T>
    {
        T Visit(ExpressionStatement expressionStatement);
    }
}