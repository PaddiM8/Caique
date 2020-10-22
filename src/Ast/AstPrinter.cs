using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;

namespace Caique.Ast
{
    class AstPrinter : IAstTraverser<object, object>
    {
        private int _indentationLevel = 1;
        private readonly AbstractSyntaxTree _ast;

        public AstPrinter(AbstractSyntaxTree ast)
        {
            _ast = ast;
        }

        private void Next(Statement statement)
        {
            ((IAstTraverser<object, object>)this).Next(statement);
        }

        private void Next(Expression expression)
        {
            ((IAstTraverser<object, object>)this).Next(expression);
        }

        public void Print()
        {
            foreach (var statement in _ast.Statements)
            {
                Next(statement);
            }
        }

        public object Visit(ExpressionStatement expressionStatement)
        {
            Next(expressionStatement.Expression);

            return null!;
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            PrintStart("Variable: " + variableDeclStatement.Identifier.Value, ConsoleColor.DarkGreen);

            if (variableDeclStatement.SpecifiedType != null)
            {
                Next(variableDeclStatement.SpecifiedType);
            }

            Next(variableDeclStatement.Value!);
            _indentationLevel--;

            return null!;
        }

        public object Visit(ReturnStatement returnStatement)
        {
            PrintStart("ret", ConsoleColor.Magenta);
            Next(returnStatement.Expression);
            _indentationLevel--;

            return null!;
        }

        public object Visit(FunctionDeclStatement functionDeclStatement)
        {
            PrintStart("Function: " + functionDeclStatement.Identifier.Value, ConsoleColor.Magenta);

            // Parameters
            PrintStart("Parameters: ", ConsoleColor.Magenta);
            foreach (var parameter in functionDeclStatement.Parameters)
                Next(parameter.Type);
            _indentationLevel--;

            if (functionDeclStatement.ReturnType != null)
                Next(functionDeclStatement.ReturnType);

            Next(functionDeclStatement.Body);
            _indentationLevel--;

            return null!;
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            PrintStart("Class: " + classDeclStatement.Identifier.Value, ConsoleColor.DarkGreen);

            foreach (var parameter in classDeclStatement.ParameterRefTokens)
                PrintMiddle(parameter.Value, ConsoleColor.Green);
            _indentationLevel--;

            Next(classDeclStatement.Body);

            if (classDeclStatement.Inherited != null)
            {
                Next(classDeclStatement.Inherited);
            }

            _indentationLevel--;

            return null!;
        }

        public object Visit(AssignmentStatement assignmentStatement)
        {
            PrintStart("=", ConsoleColor.Green);
            Next(assignmentStatement.Assignee);
            Next(assignmentStatement.Value);
            _indentationLevel--;

            return null!;
        }

        public object Visit(UseStatement useStatement)
        {
            PrintStart("use", ConsoleColor.Magenta);
            PrintMiddle(StringifyModulePath(useStatement.ModulePath), ConsoleColor.Green);
            _indentationLevel--;

            return null!;
        }

        public object Visit(UnaryExpression unaryExpression)
        {
            PrintStart(unaryExpression.Operator.Kind.ToStringRepresentation(), ConsoleColor.Green);
            Next(unaryExpression.Value);
            _indentationLevel--;

            return null!;
        }

        public object Visit(BinaryExpression binaryExpression)
        {
            PrintStart(binaryExpression.Operator.Kind.ToStringRepresentation(), ConsoleColor.Green);
            Next(binaryExpression.Left);
            Next(binaryExpression.Right);
            _indentationLevel--;

            return null!;
        }

        public object Visit(DotExpression dotExpression)
        {
            PrintStart(".", ConsoleColor.Magenta);
            Next(dotExpression.Left);
            Next(dotExpression.Right);
            _indentationLevel--;

            return null!;
        }

        public object Visit(LiteralExpression literalExpression)
        {
            PrintMiddle(literalExpression.Value.Value, ConsoleColor.Magenta);

            return null!;
        }

        public object Visit(GroupExpression groupExpression)
        {
            PrintStart("Group", ConsoleColor.Magenta);
            Next(groupExpression.Expression);
            _indentationLevel--;

            return null!;
        }

        public object Visit(BlockExpression blockStatement)
        {
            PrintStart("Block", ConsoleColor.Magenta);

            foreach (var statement in blockStatement.Statements)
                Next(statement);

            _indentationLevel--;

            return null!;
        }

        public object Visit(VariableExpression variableExpression)
        {
            PrintMiddle(variableExpression.Identifier.Value, ConsoleColor.DarkCyan);

            return null!;
        }

        public object Visit(CallExpression callExpression)
        {
            PrintStart(
                StringifyModulePath(callExpression.ModulePath),
                ConsoleColor.DarkCyan
            );

            foreach (var arg in callExpression.Arguments)
                Next(arg);

            _indentationLevel--;

            return null!;
        }

        public object Visit(NewExpression newExpression)
        {
            PrintStart("new ", ConsoleColor.Yellow);
            Next(newExpression.Type);
            foreach (var arg in newExpression.Arguments)
                Next(arg);

            _indentationLevel--;

            return null!;
        }

        public object Visit(TypeExpression typeExpression)
        {
            PrintMiddle(
                StringifyModulePath(typeExpression.ModulePath),
                ConsoleColor.DarkCyan
            );

            return null!;
        }

        public object Visit(IfExpression ifExpression)
        {
            PrintStart("If ", ConsoleColor.Blue);
            Next(ifExpression.Condition);
            Next(ifExpression.Branch);

            if (ifExpression.ElseBranch != null)
            {
                PrintStart("Else ", ConsoleColor.Blue);
                Next(ifExpression.ElseBranch);
                _indentationLevel--;
            }

            _indentationLevel--;

            return null!;
        }

        private void PrintStart(string value, ConsoleColor color)
        {
            PrintSection(value, color, "┣━ ");
            _indentationLevel++;
        }

        private void PrintMiddle(string value, ConsoleColor color)
        {
            PrintSection(value, color, "┣━ ");
        }

        private void PrintSection(string value, ConsoleColor color, string block)
        {
            Console.Write(string.Join("", Enumerable.Repeat("┃  ", _indentationLevel - 1)));
            Console.Write(block);

            Console.ForegroundColor = color;
            Console.WriteLine(value);
            Console.ResetColor();
        }

        private static string StringifyModulePath(List<Token> modulePath)
            => string.Join("->", modulePath.Select(x => x.Value));
    }
}