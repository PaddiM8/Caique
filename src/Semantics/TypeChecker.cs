using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Ast;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Util;

namespace Caique.Semantics
{
    class TypeChecker : IAstTraverser<object, DataType>
    {
        private readonly AbstractSyntaxTree _ast;
        private readonly DiagnosticBag _diagnostics;
        private SymbolEnvironment _environment;
        private TypeCheckerContext _current = new TypeCheckerContext();
        private static readonly DataType _voidType = new DataType(TypeKeyword.Void);
        private static readonly DataType _boolType = new DataType(TypeKeyword.Bool);
        private static readonly DataType _unknownType = new DataType(TypeKeyword.Unknown);

        public TypeChecker(AbstractSyntaxTree ast, DiagnosticBag diagnostics)
        {
            _ast = ast;
            _diagnostics = diagnostics;
            _environment = ast.ModuleEnvironment.SymbolEnvironment;
        }

        private void Next(Statement statement)
        {
            _current = _current.CreateChild();
            ((IAstTraverser<object, DataType>)this).Next(statement);
            _current = _current.Parent!;
        }

        private DataType Next(Expression expression)
        {
            _current = _current.CreateChild();
            var value = ((IAstTraverser<object, DataType>)this).Next(expression);
            _current = _current.Parent!;

            return value;
        }

        public void Analyse()
        {
            foreach (var statement in _ast.Statements)
                Next(statement);
        }

        public object Visit(ExpressionStatement expressionStatement)
        {
            Next(expressionStatement.Expression);

            return null!;
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            // If a type was specified
            if (variableDeclStatement.SpecifiedType != null)
            {
                var specifiedType = Next(variableDeclStatement.SpecifiedType);
                variableDeclStatement.DataType = specifiedType;

                // Make sure the value type match up with the specified type
                // if there is a value
                if (variableDeclStatement.Value != null)
                {
                    var valueType = Next(variableDeclStatement.Value!);
                    CheckTypes(
                        specifiedType,
                        valueType,
                        variableDeclStatement.Value.Span
                    );
                }
            }
            else
            {
                var valueType = Next(variableDeclStatement.Value!);
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

        public object Visit(ReturnStatement returnStatement)
        {
            var type = Next(returnStatement.Expression);
            CheckTypes(_current.FunctionType!.Value, type, returnStatement.Span);

            return null!;
        }

        public object Visit(AssignmentStatement assignmentStatement)
        {
            var variableType = Next(assignmentStatement.Variable);
            var valueType = Next(assignmentStatement.Value);

            CheckTypes(variableType, valueType);

            return null!;
        }

        public object Visit(FunctionDeclStatement functionDeclStatement)
        {
            _current.FunctionType = functionDeclStatement.ReturnType == null
                ? _voidType
                : Next(functionDeclStatement.ReturnType);

            var bodyType = Next(functionDeclStatement.Body);
            CheckTypes(
                _current.FunctionType!.Value,
                bodyType,
                functionDeclStatement.Body.Span
            );

            _current.FunctionType = null;

            return null!;
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            var ancestor = classDeclStatement.Inherited;
            if (ancestor != null)
            {
                if (ancestor.Identifier.Value == classDeclStatement.Identifier.Value)
                {
                    _diagnostics.ReportUnableToInherit(
                        ancestor.Identifier,
                        classDeclStatement.Identifier
                    );
                }
            }

            foreach (var parameterRef in classDeclStatement.ParameterRefs)
            {
                if (classDeclStatement.GetVariable(parameterRef.Value) == null)
                {
                    _diagnostics.ReportSymbolDoesNotExist(parameterRef);
                }
            }

            Next(classDeclStatement.Body);

            return null!;
        }

        public object Visit(UseStatement useStatement)
        {
            var module = _ast.ModuleEnvironment.FindByPath(
                useStatement.ModulePath
                    .Select(x => x.Value) // Convert Token into string
                    .Prepend("root") // Start looking from the root
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
            var valueType = Next(unaryExpression.Value);
            unaryExpression.DataType = valueType;
            if (!valueType.IsNumber())
            {
                _diagnostics.ReportUnexpectedType(
                    valueType,
                    "number",
                    unaryExpression.Span
                );
            }

            return valueType;
        }

        public DataType Visit(BinaryExpression binaryExpression)
        {
            var leftType = Next(binaryExpression.Left);
            var rightType = Next(binaryExpression.Right);
            binaryExpression.DataType = leftType;
            CheckTypes(leftType, rightType, binaryExpression.Span);

            return binaryExpression.Operator.Kind.IsComparisonOperator()
                ? _boolType
                : leftType;
        }

        public DataType Visit(DotExpression dotExpression)
        {
            var leftType = Next(dotExpression.Left);

            // If it's a user-made class
            if (leftType.Type == TypeKeyword.Identifier)
            {
                var classDecl = leftType.ObjectDecl!;

                // Can't continue if the class couldn't be found.
                // This will probably mostly happen due to other user-errors,
                // which will reported where relevant instead.
                if (classDecl == null) return _unknownType;

                // Check if the variable/function exists in the class
                if (dotExpression.Right is CallExpression callExpression)
                {
                    var identifier = callExpression.ModulePath[^1];
                    var function = classDecl.GetFunction(identifier.Value);
                    var type = CheckCall(identifier, function, callExpression.Arguments);
                    callExpression.DataType = type;

                    return type;
                }
                else if (dotExpression.Right is VariableExpression variableExpression)
                {
                    var identifier = variableExpression.Identifier;
                    var variable = classDecl.GetVariable(identifier.Value);
                    var type = CheckVariableDecl(identifier, variable);
                    variableExpression.DataType = type;

                    return type;
                }
                else
                {
                    throw new Exception("Expected call expression or variable expression. This should be a compiler error.");
                }
            }
            else
            {
                _diagnostics.ReportUnexpectedType(leftType, "object", dotExpression.Left.Span);
            }

            return _unknownType;
        }

        public DataType Visit(LiteralExpression literalExpression)
        {
            if (literalExpression.Value.Kind == TokenKind.NumberLiteral)
            {
                var type = new DataType(TypeKeyword.NumberLiteral);
                literalExpression.DataType = type;

                return type;
            }

            throw new NotImplementedException();
        }

        public DataType Visit(GroupExpression groupExpression)
        {
            var type = Next(groupExpression.Expression);
            groupExpression.DataType = type;

            return type;
        }

        public DataType Visit(BlockExpression blockExpression)
        {
            _environment = blockExpression.Environment;
            DataType returnType = _voidType;

            foreach (var (statement, i) in blockExpression.Statements.WithIndex())
            {
                bool isLast = i == blockExpression.Statements.Count - 1;

                // If it's an expression statement and
                // it doesn't have a trailing semicolon, it should be returned.
                if (isLast &&
                    statement is ExpressionStatement expressionStatement &&
                    !expressionStatement.TrailingSemicolon)
                {
                    returnType = Next(expressionStatement.Expression);
                }
                else
                {
                    Next(statement);
                }
            }

            _environment = _environment.Parent!;
            blockExpression.DataType = returnType;

            return returnType;
        }

        public DataType Visit(VariableExpression variableExpression)
        {
            var variableName = variableExpression.Identifier;
            var variableDecl = _environment.GetVariable(variableName.Value);
            var type = CheckVariableDecl(variableName, variableDecl);
            variableExpression.DataType = type;

            return type;
        }

        public DataType Visit(CallExpression callExpression)
        {
            var lastIdentifier = callExpression.ModulePath[^1];
            var environment = _environment;
            if (callExpression.ModulePath.Count > 1)
            {
                var module = GetModule(callExpression.ModulePath);
                if (module == null) return _unknownType;
                _environment = module.SymbolEnvironment;
            }

            var functionDecl = environment.GetFunction(lastIdentifier.Value);
            var type = CheckCall(lastIdentifier, functionDecl, callExpression.Arguments);
            callExpression.DataType = type;

            return type;
        }

        public DataType Visit(NewExpression newExpression)
        {
            var type = Next(newExpression.Type);
            var classDecl = type.ObjectDecl!;
            if (classDecl == null) return _unknownType;

            int argumentCount = newExpression.Arguments.Count;
            int parameterCount = classDecl.ParameterRefs.Count;
            if (argumentCount != parameterCount)
            {
                _diagnostics.ReportWrongNumberOfArguments(
                    classDecl.Identifier,
                    argumentCount,
                    parameterCount
                );

                return _unknownType;
            }
            foreach (var (argument, i) in newExpression.Arguments.WithIndex())
            {
                var argumentType = Next(argument);
                var varDecl = classDecl!.Body.Environment.GetVariable(
                    classDecl.ParameterRefs[i].Value
                );

                if (varDecl == null) continue;
                if (varDecl.DataType == null) Next(varDecl);

                CheckTypes(varDecl.DataType!.Value, argumentType);
            }

            newExpression.DataType = type;

            return type;
        }

        public DataType Visit(TypeExpression typeExpression)
        {
            if (typeExpression.ModulePath.Count == 1)
            {
                var keyword = typeExpression.ModulePath[0].Kind switch
                {
                    TokenKind.i8 => TypeKeyword.i8,
                    TokenKind.i32 => TypeKeyword.i32,
                    TokenKind.i64 => TypeKeyword.i64,
                    TokenKind.f8 => TypeKeyword.f8,
                    TokenKind.f32 => TypeKeyword.f32,
                    TokenKind.f64 => TypeKeyword.f64,
                    _ => TypeKeyword.Identifier,
                };

                if (keyword != TypeKeyword.Identifier)
                {
                    var dataType = new DataType(keyword);
                    typeExpression.DataType = dataType;

                    return dataType;
                }
            }

            var classDecl = _ast.ModuleEnvironment.GetClass(typeExpression.ModulePath);
            var lastIdentifier = typeExpression.ModulePath[^1];

            if (classDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(lastIdentifier);
            }

            var type = new DataType(TypeKeyword.Identifier, classDecl);
            typeExpression.DataType = type;

            return type;
        }

        public DataType Visit(IfExpression ifExpression)
        {
            var conditionType = Next(ifExpression.Condition);
            CheckTypes(_boolType, conditionType, ifExpression.Condition.Span);

            if (ifExpression.Branch is ExpressionStatement branchExprStmt &&
                branchExprStmt.Expression is BlockExpression branchBlock &&
                ifExpression.ElseBranch is ExpressionStatement elseBranchExprStmt &&
                elseBranchExprStmt.Expression is BlockExpression elseBranchBlock)
            {
                var branchType = Next(branchBlock);
                var elseBranchType = Next(elseBranchBlock);
                CheckTypes(branchType, elseBranchType, ifExpression.Span);
                ifExpression.DataType = branchType;

                return branchType;
            }

            return _voidType;
        }

        private static bool CheckTypes(DataType type1, DataType type2)
        {
            return type1.Type == type2.Type;
        }

        private ModuleEnvironment? GetModule(List<Token> modulePath)
        {
            var module = _ast.ModuleEnvironment.FindByPath(modulePath);

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

                return _unknownType;
            }

            // If it has already been checked once before,
            // the DataType will be cached (stored in the object).
            if (variableDecl.DataType != null)
                return variableDecl.DataType.Value;

            DataType type;
            if (variableDecl.SpecifiedType != null)
            {
                type = Next(variableDecl.SpecifiedType);
            }
            else
            {
                type = Next(variableDecl.Value!);
            }

            variableDecl.DataType = type;

            return type;
        }

        private DataType CheckCall(Token identifier, FunctionDeclStatement? functionDecl,
                                   List<Expression> arguments)
        {
            if (functionDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(identifier);

                return _unknownType;
            }


            var returnType = functionDecl.ReturnType == null
                ? _voidType
                : Next(functionDecl.ReturnType);

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
                var argumentType = Next(argument);
                var parameterType = Next(parameter.Type);
                CheckTypes(parameterType, argumentType, argument.Span);
            }

            return returnType;
        }

        private bool CheckTypes(DataType expected, DataType got, TextSpan span)
        {
            // This means an error has been found somewhere else,
            // so just ignore it.
            if (expected.Type == TypeKeyword.Unknown ||
                got.Type == TypeKeyword.Unknown)
                return true;

            bool compatible = got.IsCompatible(expected);
            if (!compatible)
            {
                _diagnostics.ReportUnexpectedType(got, expected, span);
            }

            return compatible;
        }
    }
}