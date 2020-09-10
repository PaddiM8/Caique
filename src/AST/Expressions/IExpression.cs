using System;

namespace Caique.AST
{
    public interface IExpression
    {
        T Accept<T>(IExpressionVisitor<T> visitor);
    }
}