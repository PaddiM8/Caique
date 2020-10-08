using System;

namespace Caique.Ast
{
    public interface IExpressionVisitor<T>
    {
        T Visit(UnaryExpression unaryExpression);
        T Visit(BinaryExpression binaryExpression);
        T Visit(LiteralExpression literalExpression);
        T Visit(GroupExpression groupExpression);
        T Visit(BlockExpression blockExpression);
        T Visit(VariableExpression variableExpression);
        T Visit(CallExpression callExpression);
        T Visit(TypeExpression typeExpression);
        T Visit(IfExpression ifExpression);
        T Visit(NewExpression newExpression);
        T Visit(DotExpression dotExpression);
    }
}