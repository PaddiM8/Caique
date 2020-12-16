using System;

namespace Caique.Ast
{
    public interface IAstTraverser<T, U>
    {
        T Visit(ExpressionStatement expressionStatement);
        T Visit(VariableDeclStatement variableDeclStatement);
        T Visit(ReturnStatement returnStatement);
        T Visit(AssignmentStatement assignmentStatement);
        T Visit(FunctionDeclStatement functionDeclStatement);
        T Visit(ClassDeclStatement classDeclStatement);
        T Visit(UseStatement useStatement);

        U Visit(UnaryExpression unaryExpression);
        U Visit(BinaryExpression binaryExpression);
        U Visit(LiteralExpression literalExpression);
        U Visit(GroupExpression groupExpression);
        U Visit(BlockExpression blockExpression);
        U Visit(VariableExpression variableExpression);
        U Visit(CallExpression callExpression);
        U Visit(TypeExpression typeExpression);
        U Visit(IfExpression ifExpression);
        U Visit(NewExpression newExpression);
        U Visit(DotExpression dotExpression);
        U Visit(SelfExpression selfExpression);

        public T Next(Statement statement)
        {
            return statement switch
            {
                ExpressionStatement toVisit => Visit(toVisit),
                VariableDeclStatement toVisit => Visit(toVisit),
                ReturnStatement toVisit => Visit(toVisit),
                AssignmentStatement toVisit => Visit(toVisit),
                FunctionDeclStatement toVisit => Visit(toVisit),
                ClassDeclStatement toVisit => Visit(toVisit),
                UseStatement toVisit => Visit(toVisit),
                _ => throw new Exception(statement.ToString()),
            };
            ;
        }

        public U Next(Expression expression)
        {
            return expression switch
            {
                UnaryExpression toVisit => Visit(toVisit),
                BinaryExpression toVisit => Visit(toVisit),
                DotExpression toVisit => Visit(toVisit),
                LiteralExpression toVisit => Visit(toVisit),
                GroupExpression toVisit => Visit(toVisit),
                BlockExpression toVisit => Visit(toVisit),
                VariableExpression toVisit => Visit(toVisit),
                CallExpression toVisit => Visit(toVisit),
                NewExpression toVisit => Visit(toVisit),
                TypeExpression toVisit => Visit(toVisit),
                IfExpression toVisit => Visit(toVisit),
                SelfExpression toVisit => Visit(toVisit),
                _ => throw new Exception(expression.ToString()),
            };
        }
    }
}