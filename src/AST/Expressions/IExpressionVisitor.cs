using System;

namespace Caique.AST
{
    public interface IExpressionVisitor<T>
    {
        T Visit(BinaryExpression binaryExpression);
        T Visit(LiteralExpression literalExpression);
    }
}