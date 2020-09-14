using System;

namespace Caique.AST
{
    public interface IStatementVisitor<T>
    {
        T Visit(ExpressionStatement expressionStatement);
        T Visit(VariableDeclStatement variableDeclStatement);
        T Visit(AssignmentStatement assignmentStatement);
        T Visit(FunctionDeclStatement functionDeclStatement);
        T Visit(ClassDeclStatement classDeclStatement);
    }
}