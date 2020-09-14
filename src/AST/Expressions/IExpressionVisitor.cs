using System;

namespace Caique.AST
{
    public interface IExpressionVisitor<T>
    {
        T Visit(UnaryExpression unaryExpression);
        T Visit(BinaryExpression binaryExpression);
        T Visit(LiteralExpression literalExpression);
        T Visit(GroupExpression groupExpression);
        T Visit(VariableExpression variableExpression);
        T Visit(CallExpression callExpression);
        T Visit(TypeExpression typeExpression);
        T Visit(IfExpression ifExpression);
    }
}