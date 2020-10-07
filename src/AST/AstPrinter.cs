﻿using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;

namespace Caique.AST
{
    class AstPrinter : IStatementVisitor<object>, IExpressionVisitor<object>
    {
        private int _indentationLevel = 1;
        private Ast _ast;

        public AstPrinter(Ast ast)
        {
            _ast = ast;
        }

        public void Print()
        {
            foreach (var statement in _ast.Statements)
            {
                statement.Accept(this);
            }
        }

        public object Visit(ExpressionStatement expressionStatement)
        {
            expressionStatement.Expression.Accept(this);

            return null!;
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            PrintStart("Variable: " + variableDeclStatement.Identifier.Value, ConsoleColor.DarkGreen);

            if (variableDeclStatement.SpecifiedType != null)
            {
                variableDeclStatement.SpecifiedType.Accept(this);
            }

            variableDeclStatement.Value!.Accept(this);
            _indentationLevel--;

            return null!;
        }

        public object Visit(ReturnStatement returnStatement)
        {
            PrintStart("ret", ConsoleColor.Magenta);
            returnStatement.Expression.Accept(this);
            _indentationLevel--;

            return null!;
        }

        public object Visit(FunctionDeclStatement functionDeclStatement)
        {
            PrintStart("Function: " + functionDeclStatement.Identifier.Value, ConsoleColor.Magenta);

            // Parameters
            PrintStart("Parameters: ", ConsoleColor.Magenta);
            foreach (var parameter in functionDeclStatement.Parameters)
                parameter.Type.Accept(this);
            _indentationLevel--;

            if (functionDeclStatement.ReturnType != null)
                functionDeclStatement.ReturnType.Accept(this);

            functionDeclStatement.Body.Accept(this);
            _indentationLevel--;

            return null!;
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            PrintStart("Class: " + classDeclStatement.Identifier.Value, ConsoleColor.DarkGreen);

            foreach (var parameter in classDeclStatement.ParameterRefs)
                PrintMiddle(parameter.Value, ConsoleColor.Green);
            _indentationLevel--;

            classDeclStatement.Body.Accept(this);

            if (classDeclStatement.Inherited != null)
            {
                classDeclStatement.Inherited.Accept(this);
            }

            _indentationLevel--;

            return null!;
        }

        public object Visit(AssignmentStatement assignmentStatement)
        {
            PrintStart(assignmentStatement.Operator.Kind.ToStringRepresentation(), ConsoleColor.Green);
            assignmentStatement.Variable.Accept(this);
            assignmentStatement.Value.Accept(this);
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
            unaryExpression.Value.Accept(this);
            _indentationLevel--;

            return null!;
        }

        public object Visit(BinaryExpression binaryExpression)
        {
            PrintStart(binaryExpression.Operator.Kind.ToStringRepresentation(), ConsoleColor.Green);
            binaryExpression.Left.Accept(this);
            binaryExpression.Right.Accept(this);
            _indentationLevel--;

            return null!;
        }

        public object Visit(DotExpression dotExpression)
        {
            PrintStart(".", ConsoleColor.Magenta);
            dotExpression.Left.Accept(this);
            dotExpression.Right.Accept(this);
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
            groupExpression.Expression.Accept(this);
            _indentationLevel--;

            return null!;
        }

        public object Visit(BlockExpression blockStatement)
        {
            PrintStart("Block", ConsoleColor.Magenta);

            foreach (var statement in blockStatement.Statements)
                statement.Accept(this);

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
                arg.Accept(this);

            _indentationLevel--;

            return null!;
        }

        public object Visit(NewExpression newExpression)
        {
            PrintStart("new ", ConsoleColor.Yellow);
            newExpression.Type.Accept(this);
            foreach (var arg in newExpression.Arguments)
                arg.Accept(this);

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
            ifExpression.Condition.Accept(this);
            ifExpression.Branch.Accept(this);

            if (ifExpression.ElseBranch != null)
            {
                PrintStart("Else ", ConsoleColor.Blue);
                ifExpression.ElseBranch.Accept(this);
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

        private string StringifyModulePath(List<Token> modulePath)
            => string.Join("->", modulePath.Select(x => x.Value));
    }
}