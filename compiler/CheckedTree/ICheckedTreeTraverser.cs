using System;

namespace Caique.CheckedTree
{
    public interface ICheckedTreeTraverser<T, U>
    {
        T Visit(CheckedExpressionStatement expressionStatement);
        T Visit(CheckedVariableDeclStatement variableDeclStatement);
        T Visit(CheckedReturnStatement returnStatement);
        T Visit(CheckedAssignmentStatement assignmentStatement);
        T Visit(CheckedFunctionDeclStatement functionDeclStatement);
        T Visit(CheckedClassDeclStatement classDeclStatement);
        T Visit(CheckedWhileStatement whileStatement);
        T Visit(CheckedUseStatement useStatement);

        U Visit(CheckedUnaryExpression unaryExpression);
        U Visit(CheckedBinaryExpression binaryExpression);
        U Visit(CheckedLiteralExpression literalExpression);
        U Visit(CheckedGroupExpression groupExpression);
        U Visit(CheckedBlockExpression blockExpression);
        U Visit(CheckedVariableExpression variableExpression);
        U Visit(CheckedCallExpression callExpression);
        U Visit(CheckedTypeExpression typeExpression);
        U Visit(CheckedIfExpression ifExpression);
        U Visit(CheckedNewExpression newExpression);
        U Visit(CheckedDotExpression dotExpression);
        U Visit(CheckedKeywordValueExpression selfExpression);

        public T Next(CheckedStatement statement)
        {
            return statement switch
            {
                CheckedExpressionStatement toVisit => Visit(toVisit),
                CheckedVariableDeclStatement toVisit => Visit(toVisit),
                CheckedReturnStatement toVisit => Visit(toVisit),
                CheckedAssignmentStatement toVisit => Visit(toVisit),
                CheckedFunctionDeclStatement toVisit => Visit(toVisit),
                CheckedClassDeclStatement toVisit => Visit(toVisit),
                CheckedWhileStatement toVisit => Visit(toVisit),
                CheckedUseStatement toVisit => Visit(toVisit),
                _ => throw new Exception(statement.ToString()),
            };
        }

        public U Next(CheckedExpression expression)
        {
            return expression switch
            {
                CheckedUnaryExpression toVisit => Visit(toVisit),
                CheckedBinaryExpression toVisit => Visit(toVisit),
                CheckedDotExpression toVisit => Visit(toVisit),
                CheckedLiteralExpression toVisit => Visit(toVisit),
                CheckedGroupExpression toVisit => Visit(toVisit),
                CheckedBlockExpression toVisit => Visit(toVisit),
                CheckedVariableExpression toVisit => Visit(toVisit),
                CheckedCallExpression toVisit => Visit(toVisit),
                CheckedNewExpression toVisit => Visit(toVisit),
                CheckedTypeExpression toVisit => Visit(toVisit),
                CheckedIfExpression toVisit => Visit(toVisit),
                CheckedKeywordValueExpression toVisit => Visit(toVisit),
                _ => throw new Exception(expression.ToString()),
            };
        }
    }
}