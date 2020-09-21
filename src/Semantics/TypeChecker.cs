using System;
using System.Collections.Generic;
using System.Linq;
using Caique.AST;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Util;

namespace Caique.Semantics
{
    class TypeChecker : IStatementVisitor<object>, IExpressionVisitor<DataType>
    {
        private readonly Ast _ast;
        private readonly DiagnosticBag _diagnostics;
        private SymbolEnvironment _environment;
        private static DataType _voidType = new DataType(TypeKeyword.Void);

        public TypeChecker(Ast ast, DiagnosticBag diagnostics)
        {
            _ast = ast;
            _diagnostics = diagnostics;
            _environment = ast.ModuleEnvironment.SymbolEnvironment;
        }

        public void Analyse()
        {
            foreach (var statement in _ast.Statements)
                statement.Accept(this);
        }

        public object Visit(ExpressionStatement expressionStatement)
        {
            expressionStatement.Expression.Accept(this);

            return null!;
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            // If a type was specified
            if (variableDeclStatement.SpecifiedType != null)
            {
                var specifiedType = variableDeclStatement.SpecifiedType.Accept(this);
                variableDeclStatement.DataType = specifiedType;

                // Make sure the value type match up with the specified type
                // if there is a value
                if (variableDeclStatement.Value != null)
                {
                    var valueType = variableDeclStatement.Value!.Accept(this);
                    if (specifiedType.Type != valueType.Type)
                    {
                        _diagnostics.ReportUnexpectedType(valueType, specifiedType);
                    }
                }
            }
            else
            {
                var valueType = variableDeclStatement.Value!.Accept(this);
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

        public object Visit(UseStatement useStatement)
        {
            var module = _ast.ModuleEnvironment.Root.FindByPath(
                useStatement.ModulePath.Select(x => x.Value)
            );

            if (module != null)
            {
                _ast.ModuleEnvironment.ImportModule(module);
            }
            else
            {
                _diagnostics.ReportInvalidModulePath(useStatement.ModulePath);
            }

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

        public DataType Visit(DotExpression dotExpression)
        {
            var leftType = dotExpression.Left.Accept(this);

            // If it's a user-made class
            if (leftType.Type == TypeKeyword.Identifier)
            {
                var symbolEnvironment = leftType.Module!
                    .GetClass(leftType.Identifier!.Value)!.Body.Environment;

                // Check if the variable/function exists in the class
                if (dotExpression.Right is CallExpression callExpression)
                {
                    var identifier = callExpression.ModulePath[^1];
                    var function = symbolEnvironment.GetFunction(identifier.Value);

                    return CheckCall(identifier, function, callExpression.Arguments);
                }
                else if (dotExpression.Right is VariableExpression variableExpression)
                {
                    var identifier = variableExpression.Identifier;
                    var variable = symbolEnvironment.GetVariable(identifier.Value);

                    return CheckVariableDecl(identifier, variable);
                }
                else
                {
                    throw new Exception("Expected call expression or variable expression. This should be a compiler error, but there is currently no way of getting the token for plain IExpressions. This will be implemented later.");
                }
            }
            else
            {
                _diagnostics.ReportUnexpectedType(leftType, "object");
            }

            return new DataType(TypeKeyword.Unknown);
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
            var variableName = variableExpression.Identifier;
            var variableDecl = _environment.GetVariable(variableName.Value);

            return CheckVariableDecl(variableName, variableDecl);
        }

        public DataType Visit(CallExpression callExpression)
        {
            FunctionDeclStatement? functionDecl;
            var lastIdentifier = callExpression.ModulePath[^1];
            if (callExpression.ModulePath.Count > 1)
            {
                var module = GetModule(callExpression.ModulePath);
                if (module == null) return new DataType(TypeKeyword.Unknown);

                functionDecl = module.SymbolEnvironment.GetFunction(lastIdentifier.Value);
            }
            else
            {
                functionDecl = _environment.GetFunction(lastIdentifier.Value);
            }

            return CheckCall(lastIdentifier, functionDecl, callExpression.Arguments);
        }

        public DataType Visit(NewExpression newExpression)
        {
            var module = _ast.ModuleEnvironment;
            var lastIdentifier = newExpression.ModulePath[^1];
            if (newExpression.ModulePath.Count > 1)
            {
                module = GetModule(newExpression.ModulePath);
            }

            if (module != null)
            {
                var classDecl = module.GetClass(lastIdentifier.Value);
                if (classDecl == null)
                {
                    _diagnostics.ReportSymbolDoesNotExist(lastIdentifier);
                }
            }

            return new DataType(TypeKeyword.Identifier, lastIdentifier, module);
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

        private ModuleEnvironment? GetModule(List<Token> modulePath)
        {
            var module = _ast.ModuleEnvironment.FindByPath(
                // Turn List<Token> into List<string>
                modulePath.Select(x => x.Value)
            );

            if (module == null)
            {
                _diagnostics.ReportInvalidModulePath(modulePath);
            }

            return module;
        }

        private DataType CheckVariableDecl(Token identifier, VariableDeclStatement? variableDecl)
        {
            if (variableDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(identifier);

                return new DataType(TypeKeyword.Unknown);
            }

            // If it has already been checked once before,
            // the DataType will be cached (stored in the object).
            if (variableDecl.DataType != null)
                return variableDecl.DataType.Value;

            DataType type;
            if (variableDecl.SpecifiedType != null)
            {
                type = variableDecl.SpecifiedType.Accept(this);
            }
            else
            {
                type = variableDecl.Value!.Accept(this);
            }

            variableDecl.DataType = type;

            return type;
        }

        private DataType CheckCall(Token identifier, FunctionDeclStatement? functionDecl,
                                   List<IExpression> arguments)
        {
            if (functionDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(identifier);

                return new DataType(TypeKeyword.Unknown);
            }


            var returnType = functionDecl.ReturnType == null
                ? _voidType
                : functionDecl.ReturnType.Accept(this);

            // If wrong number of arguments
            if (arguments.Count != functionDecl.Parameters.Count)
            {
                _diagnostics.ReportWrongNumberOfArguments(
                    identifier,
                    arguments.Count,
                    functionDecl.Parameters.Count
                );

                return returnType;
            }

            foreach (var (argument, parameter) in
                     arguments.Zip(functionDecl.Parameters))
            {
                var argumentType = argument.Accept(this);
                var parameterType = parameter.Type.Accept(this);
                if (!argumentType.IsCompatible(parameterType))
                {
                    _diagnostics.ReportUnexpectedType(argumentType, parameterType);
                }
            }

            return returnType;
        }
    }
}