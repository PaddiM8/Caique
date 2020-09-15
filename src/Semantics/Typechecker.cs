using System;
using System.Collections.Generic;
using System.Linq;
using Caique.AST;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Util;

namespace Caique.Semantics
{
    class Typechecker : IStatementVisitor<object>, IExpressionVisitor<DataType>
    {
        private readonly List<IStatement> _statements;
        private readonly DiagnosticBag _diagnostics;
        private SymbolEnvironment _environment;
        private static DataType _voidType = new DataType(TypeKeyword.Void);

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

            // If a type was specified
            if (variableDeclStatement.SpecifiedType != null)
            {
                var specifiedType = variableDeclStatement.SpecifiedType.Accept(this);
                variableDeclStatement.DataType = specifiedType;

                if (valueType.Type == TypeKeyword.Unknown)
                {
                    _diagnostics.ReportUnableToInferType(variableDeclStatement.Identifier.Span);
                }
                else if (specifiedType.Type != valueType.Type)
                {
                    _diagnostics.ReportUnexpectedType(valueType, specifiedType);
                }
            }
            else
            {
                variableDeclStatement.DataType = valueType;
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
            var variableType = assignmentStatement.Variable.Accept(this);
            var valueType = assignmentStatement.Value.Accept(this);

            CheckTypes(variableType, valueType);

            return null!;
        }

        public object Visit(FunctionDeclStatement functionDeclStatement)
        {
            functionDeclStatement.Body.Accept(this);

            return null!;
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            classDeclStatement.Body.Accept(this);

            return null!;
        }

        public DataType Visit(UnaryExpression unaryExpression)
        {
            var valueType = unaryExpression.Value.Accept(this);
            if (!valueType.IsNumber())
            {
                _diagnostics.ReportUnexpectedType(valueType, "number");
            }

            return valueType;
        }

        public DataType Visit(BinaryExpression binaryExpression)
        {
            var leftType = binaryExpression.Left.Accept(this);
            var rightType = binaryExpression.Left.Accept(this);

            if (!leftType.IsCompatible(rightType))
            {
                _diagnostics.ReportUnexpectedType(leftType, rightType);
            }

            return leftType;
        }

        public DataType Visit(LiteralExpression literalExpression)
        {
            if (literalExpression.Value.Kind == TokenKind.NumberLiteral)
            {
                return new DataType(TypeKeyword.i32);
            }

            throw new NotImplementedException();
        }

        public DataType Visit(GroupExpression groupExpression)
        {
            return groupExpression.Expression.Accept(this);
        }

        public DataType Visit(BlockExpression blockStatement)
        {
            _environment = blockStatement.Environment;
            DataType returnType = _voidType;

            foreach (var (statement, i) in blockStatement.Statements.WithIndex())
            {
                bool isLast = i == blockStatement.Statements.Count - 1;

                // If it's an expression statement and
                // it doesn't have a trailing semicolon, it should be returned.
                if (isLast &&
                    statement is ExpressionStatement expressionStatement &&
                    expressionStatement.TrailingSemicolon)
                {
                    returnType = expressionStatement.Expression.Accept(this);
                }
                else
                {
                    statement.Accept(this);
                }
            }

            _environment = _environment.Parent!;

            return returnType;
        }

        public DataType Visit(VariableExpression variableExpression)
        {
            var variableName = variableExpression.Identifiers[0];
            var variableDecl = _environment.GetVariable(variableName.Value);

            if (variableDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(variableName);

                return new DataType(TypeKeyword.Unknown);
            }

            return variableDecl.DataType!.Value;
        }

        public DataType Visit(CallExpression callExpression)
        {
            var functionDecl = _environment.GetFunction(callExpression.Identifier.Value);

            if (functionDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(callExpression.Identifier);

                return new DataType(TypeKeyword.Unknown);
            }

            foreach (var (argument, parameter) in
                     callExpression.Arguments.Zip(functionDecl.Parameters))
            {
                var argumentType = argument.Accept(this);
                var parameterType = parameter.Type.Accept(this);
                if (!argumentType.IsCompatible(parameterType))
                {
                    _diagnostics.ReportUnexpectedType(argumentType, parameterType);
                }
            }

            return functionDecl.ReturnType == null
                ? _voidType
                : functionDecl.ReturnType.Accept(this);
        }

        public DataType Visit(TypeExpression typeExpression)
        {
            var keyword = typeExpression.Identifier.Kind switch
            {
                TokenKind.i8 => TypeKeyword.i8,
                TokenKind.i32 => TypeKeyword.i32,
                TokenKind.i64 => TypeKeyword.i64,
                TokenKind.f8 => TypeKeyword.f8,
                TokenKind.f32 => TypeKeyword.f32,
                TokenKind.f64 => TypeKeyword.f64,
                _ => TypeKeyword.Unknown,
            };

            return new DataType(keyword);
        }

        public DataType Visit(IfExpression ifExpression)
        {
            var conditionType = ifExpression.Condition.Accept(this);
            var boolType = new DataType(TypeKeyword.Bool);
            if (!conditionType.IsCompatible(boolType))
            {
                _diagnostics.ReportUnexpectedType(boolType, conditionType);
            }

            if (ifExpression.Branch is ExpressionStatement branchExpressionStatement &&
                branchExpressionStatement.Expression is BlockExpression branchBlock)
            {
                return branchBlock.Accept(this);
            }

            return _voidType;
        }

        private bool CheckTypes(DataType type1, DataType type2)
        {
            return type1.Type == type2.Type;
        }
    }
}