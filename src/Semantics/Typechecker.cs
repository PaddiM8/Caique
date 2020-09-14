using System;
using System.Collections.Generic;
using Caique.AST;
using Caique.Diagnostics;
using Caique.Util;

namespace Caique.Semantics
{
    class Typechecker : IStatementVisitor<object>, IExpressionVisitor<ValueType>
    {
        private readonly List<IStatement> _statements;
        private readonly DiagnosticBag _diagnostics;
        private SymbolEnvironment _environment;
        private static ValueType _voidType = new ValueType(TypeKeyword.Void);

        public Typechecker(List<IStatement> statements, DiagnosticBag diagnostics,
                           SymbolEnvironment environment)
        {
            _statements = statements;
            _diagnostics = diagnostics;
            _environment = environment;
        }

        public void Analyse()
        {
            foreach (var statement in _statements)
                statement.Accept(this);
        }

        public object Visit(ExpressionStatement expressionStatement)
        {
            expressionStatement.Expression.Accept(this);

            return null!;
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            var valueType = variableDeclStatement.Value.Accept(this);
            variableDeclStatement.ValueType = valueType;

            // If a type was specified
            if (variableDeclStatement.SpecifiedType != null)
            {
                var specifiedType = variableDeclStatement.SpecifiedType.Accept(this);
                if (specifiedType.Type == TypeKeyword.Void)
                {
                    _diagnostics.ReportUnableToInferType(variableDeclStatement.Identifier.Span);
                }
                else if (specifiedType.Type != valueType.Type)
                {
                    _diagnostics.ReportUnexpectedType(specifiedType, valueType);
                }
            }

            try
            {
                _environment.Add(variableDeclStatement);
            }
            catch (ArgumentException)
            {
                _diagnostics.ReportSymbolAlreadyExists(variableDeclStatement.Identifier);
            }

            return null!;
        }

        public object Visit(AssignmentStatement assignmentStatement)
        {
            throw new NotImplementedException();
        }

        public object Visit(FunctionDeclStatement functionDeclStatement)
        {
            throw new NotImplementedException();
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(UnaryExpression unaryExpression)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(BinaryExpression binaryExpression)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(LiteralExpression literalExpression)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(GroupExpression groupExpression)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(BlockExpression blockStatement)
        {
            _environment = blockStatement.Environment;
            ValueType returnType = _voidType;

            foreach (var (statement, i) in blockStatement.Statements.WithIndex())
            {
                // If at last statement
                if (i == blockStatement.Statements.Count - 1)
                {
                    // If it's an expression statement and
                    // it doesn't have a trailing semicolon, it should be returned.
                    if (statement is ExpressionStatement expressionStatement &&
                        !expressionStatement.TrailingSemicolon)
                    {
                        returnType = expressionStatement.Expression.Accept(this);
                    }
                }
            }

            _environment = _environment.Parent!;

            return returnType;
        }

        public ValueType Visit(VariableExpression variableExpression)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(CallExpression callExpression)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(TypeExpression typeExpression)
        {
            throw new NotImplementedException();
        }

        public ValueType Visit(IfExpression ifExpression)
        {
            throw new NotImplementedException();
        }
    }
}