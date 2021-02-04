using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caique.Ast;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Util;

namespace Caique.Semantics
{
    class TypeChecker : IAstTraverser<object, DataType>
    {
        private readonly ModuleEnvironment _module;
        private readonly DiagnosticBag _diagnostics;
        private SymbolEnvironment _environment;
        private TypeCheckerContext _current = new();
        private ClassDeclStatement? _stringObj;
        private static readonly DataType _voidType = new DataType(TypeKeyword.Void);
        private static readonly DataType _boolType = new DataType(TypeKeyword.Bool);
        private static readonly DataType _unknownType = new DataType(TypeKeyword.Unknown);

        public TypeChecker(ModuleEnvironment module)
        {
            _module = module;
            _diagnostics = module.Diagnostics;
            _environment = module.SymbolEnvironment;

            if (module.Prelude != null)
            {
                _stringObj = module.Prelude.Modules["string"].GetClass("String")!;
            }
        }

        private void Next(Statement statement)
        {
            _current = _current.CreateChild();
            ((IAstTraverser<object, DataType>)this).Next(statement);
            _current = _current.Parent!;
        }

        private DataType Next(Expression expression, DataType? expectedType = null)
        {
            _current = _current.CreateChild(expression);
            _current.ExpectedType = expectedType ?? _current.Parent!.Expression?.DataType;
            _current.DataType = _current.Parent!.DataType; // Expressions should carry on the infer-type
            var value = ((IAstTraverser<object, DataType>)this).Next(expression);
            _current = _current.Parent!;

            return value;
        }

        public void Analyse()
        {
            foreach (var statement in _module.Ast!)
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
                _current.DataType = specifiedType;

                // Make sure the value type match up with the specified type
                // if there is a value
                if (variableDeclStatement.Value != null)
                {
                    var valueType = Next(variableDeclStatement.Value!, specifiedType);
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

            if (variableDeclStatement.VariableType == VariableType.Local)
            {
                if (!_environment.TryAdd(variableDeclStatement))
                    _diagnostics.ReportSymbolAlreadyExists(variableDeclStatement.Identifier);
            }

            return null!;
        }

        public object Visit(ReturnStatement returnStatement)
        {
            _current.DataType = _current.CurrentFunctionType;
            var type = Next(returnStatement.Expression, _current.CurrentFunctionType);
            returnStatement.DataType = CheckTypes(_current.CurrentFunctionType!, type, returnStatement.Span)
                ? type
                : _unknownType;

            return null!;
        }

        public object Visit(AssignmentStatement assignmentStatement)
        {
            var variableType = Next(assignmentStatement.Assignee);
            _current.DataType = variableType;
            var valueType = Next(assignmentStatement.Value, variableType);

            CheckTypes(variableType, valueType, assignmentStatement.Span);

            assignmentStatement.Assignee.DataType = variableType;
            assignmentStatement.Value.DataType = variableType;

            // Go through the asignee and make sure it can be assigned to
            foreach (var expression in assignmentStatement.Assignee.Expressions)
            {
                if (!(expression is VariableExpression))
                {
                    _diagnostics.ReportMisplacedAssignmentOperator(assignmentStatement.Span);
                    break;
                }
            }

            return null!;
        }

        public object Visit(FunctionDeclStatement functionDeclStatement)
        {
            _current.CurrentFunctionType = functionDeclStatement.ReturnType == null
                ? _voidType
                : Next(functionDeclStatement.ReturnType);
            _current.DataType = _current.CurrentFunctionType;

            if (functionDeclStatement.IsExtensionFunction)
            {
                _current.CurrentExtendedType = Next(functionDeclStatement.ExtensionOf!);
            }

            if (_current.CurrentObject != null)
            {
                functionDeclStatement.ParentObject = _current.CurrentObject;
            }

            // Parameters
            foreach (var parameter in functionDeclStatement.Parameters)
            {
                if (parameter.Type!.DataType == null)
                {
                    parameter.Type!.DataType = Next(parameter.Type!);
                }

                parameter.DataType = parameter.Type!.DataType;
            }

            if (functionDeclStatement.Body != null)
            {
                var bodyType = Next(functionDeclStatement.Body);
                CheckTypes(
                    _current.CurrentFunctionType!,
                    bodyType,
                    functionDeclStatement.Body.Span
                );
            }

            return null!;
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            _current.CurrentObject = classDeclStatement;
            classDeclStatement.DataType = new DataType(
                TypeKeyword.Identifier,
                classDeclStatement
            );

            if (classDeclStatement.InheritedType != null)
                Next(classDeclStatement.InheritedType);

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

            Next(classDeclStatement.Body);

            // Constructor
            if (classDeclStatement.InitFunction != null)
            {
                classDeclStatement.InitFunction.ParentObject = classDeclStatement;
                foreach (var parameter in classDeclStatement.InitFunction.Parameters)
                {
                    if (parameter.IsReference)
                    {
                        var variableDecl = classDeclStatement.GetVariable(parameter.Identifier.Value);
                        if (variableDecl == null)
                        {
                            _diagnostics.ReportSymbolDoesNotExist(parameter.Identifier);
                        }
                        else
                        {
                            parameter.DataType = variableDecl.DataType;
                        }
                    }
                }

                var initBodyType = Next(classDeclStatement.InitFunction.Body!);
                CheckTypes(
                    initBodyType,
                    _voidType,
                    classDeclStatement.InitFunction.Body.Span
                );
            }

            return null!;
        }

        public object Visit(UseStatement useStatement)
        {
            var module = _module.Parent!.FindByPath(useStatement.ModulePath);

            if (module != null)
            {
                _module.ImportModule(module);
                if (_module.Root.Identifier == "prelude" && module.Identifier == "string")
                {
                    _stringObj = module.GetClass("String");
                }
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

            if (!valueType.IsNumber)
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
            binaryExpression.DataType = leftType;
            _current.DataType = leftType;
            var rightType = Next(binaryExpression.Right);
            CheckTypes(leftType, rightType, binaryExpression.Span);

            return binaryExpression.Operator.Kind.IsComparisonOperator()
                ? _boolType
                : leftType;
        }

        public DataType Visit(DotExpression dotExpression)
        {
            var leftType = Next(dotExpression.Expressions.First());
            foreach (var right in dotExpression.Expressions.Skip(1))
            {
                if (right is CallExpression extensionFunctionCall)
                {
                    var identifier = extensionFunctionCall.ModulePath[^1];
                    var extensionFunction = _module.GetFunction($"{leftType}.{identifier.Value}");
                    if (extensionFunction != null &&
                        extensionFunction.IsExtensionFunction &&
                        leftType.IsCompatible(Next(extensionFunction.ExtensionOf!)))
                    {
                        var type = CheckCall(identifier, extensionFunction, extensionFunctionCall.Arguments);
                        extensionFunctionCall.DataType = type;
                        extensionFunctionCall.FunctionDecl = extensionFunction;

                        return type;
                    }
                }

                // If it's not an object
                if (leftType.Type != TypeKeyword.Identifier)
                {
                    _diagnostics.ReportUnexpectedType(leftType, "object", right.Span);
                    return _unknownType;
                }

                var classDecl = leftType.ObjectDecl!;

                // Can't continue if the class couldn't be found.
                // This will probably mostly happen due to other user-errors,
                // which will reported where relevant instead.
                if (classDecl == null) return _unknownType;

                // Check if the variable/function exists in the class
                if (right is CallExpression callExpression)
                {
                    var identifier = callExpression.ModulePath[^1];
                    var function = classDecl.GetFunction(identifier.Value);
                    var type = CheckCall(identifier, function, callExpression.Arguments);
                    callExpression.DataType = type;
                    callExpression.FunctionDecl = function;
                    leftType = type;
                }
                else if (right is VariableExpression variableExpression)
                {
                    var identifier = variableExpression.Identifier;
                    var variable = classDecl.GetVariable(identifier.Value);
                    var type = CheckVariableDecl(identifier, variable);
                    variableExpression.DataType = type;
                    variableExpression.VariableDecl = variable;
                    leftType = type;
                }
                else
                {
                    _diagnostics.ReportUnexpectedToken(
                        new Token(TokenKind.Identifier, right.GetType().Name, right.Span),
                        "call expression or variable expression"
                    );

                    return _unknownType;
                }
            }

            return leftType;
        }

        public DataType Visit(LiteralExpression literalExpression)
        {
            DataType type;
            if (literalExpression.Value.Kind == TokenKind.NumberLiteral)
            {
                bool isFloat = literalExpression.Value.Value.Contains(".");
                type = _current.ExpectedType ?? new DataType(
                    isFloat ? TypeKeyword.f32 : TypeKeyword.i32
                );
            }
            else if (literalExpression.Value.Kind == TokenKind.StringLiteral)
            {
                type = _current.ExpectedType?.Type == TypeKeyword.i8 && _current.ExpectedType.IsExplicitPointer
                    ? new DataType(TypeKeyword.i8, null, true)
                    : new DataType(TypeKeyword.Identifier, _stringObj);
            }
            else if (literalExpression.Value.Kind == TokenKind.CharLiteral)
            {
                type = new DataType(TypeKeyword.i8);
            }
            else
            {
                throw new NotImplementedException();
            }

            literalExpression.DataType = type;

            return type;
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
                    blockExpression.ReturnsLastExpression = true;
                    _current.DataType = _current.Parent!.DataType;
                    returnType = Next(expressionStatement.Expression);
                }
                else if (statement is ReturnStatement returnStatement)
                {
                    Next(returnStatement);
                    returnType = returnStatement.DataType!;
                    _current.DataType = returnType;
                    break;
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
            variableExpression.VariableDecl = variableDecl;

            return type;
        }

        public DataType Visit(CallExpression callExpression)
        {
            var modulePath = callExpression.ModulePath;
            var lastIdentifier = modulePath[^1];
            var module = _module;
            if (modulePath.Count > 1)
            {
                var modulePathWithoutIdentifier = modulePath.GetRange(
                    0,
                    modulePath.Count - 1
                );

                // If it's a path like someModule->someFunction(),
                // start by looking in the current module's imported modules.
                if (modulePath.Count == 2)
                {
                    var importedModule = _module.ImportedModules
                        .Where(x => x.Identifier == modulePath[0].Value)
                        .FirstOrDefault();
                    module = importedModule ?? GetModule(modulePathWithoutIdentifier);
                }
                else module = GetModule(modulePathWithoutIdentifier);

                if (module == null) return _unknownType;
            }

            FunctionDeclStatement? functionDecl;
            if (_environment.ParentObject != null && modulePath.Count == 1)
            {
                functionDecl = _environment.ParentObject?.GetFunction(lastIdentifier.Value) ??
                    module.GetFunction(lastIdentifier.Value, false);
            }
            else
            {
                functionDecl = module.GetFunction(
                    lastIdentifier.Value,
                    modulePath.Count > 1 // Look in imports if a proper module path is specified
                );
            }

            var type = CheckCall(lastIdentifier, functionDecl, callExpression.Arguments);
            callExpression.FunctionDecl = functionDecl;
            callExpression.DataType = type;

            return type;
        }

        public DataType Visit(NewExpression newExpression)
        {
            var type = Next(newExpression.Type);
            newExpression.DataType = type;

            var classDecl = type.ObjectDecl!;
            if (classDecl == null) return _unknownType;

            int argumentCount = newExpression.Arguments.Count;
            int parameterCount = classDecl.InitFunction?.Parameters.Count ?? 0;
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
                var varDecl = classDecl!.Body.Environment.GetVariable(
                    classDecl.InitFunction!.Parameters[i].Identifier.Value
                );

                if (varDecl == null) continue;
                if (varDecl.DataType == null) Next(varDecl);
                var argumentType = Next(argument, varDecl.DataType!);

                CheckTypes(varDecl.DataType!, argumentType, argument.Span);
            }

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
                    var dataType = new DataType(
                        keyword,
                        null,
                        typeExpression.IsExplicitPointer
                    );
                    typeExpression.DataType = dataType;

                    return dataType;
                }
            }

            var lastIdentifier = typeExpression.ModulePath[^1];
            var module = _module;
            if (typeExpression.ModulePath.Count > 1)
            {
                module = GetModule(typeExpression.ModulePath);
                if (module == null) return _unknownType;
            }

            var classDecl = module.GetClass(lastIdentifier.Value);
            if (classDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(lastIdentifier);
            }

            var type = new DataType(
                TypeKeyword.Identifier,
                classDecl,
                typeExpression.IsExplicitPointer
            );
            typeExpression.DataType = type;

            return type;
        }

        public DataType Visit(IfExpression ifExpression)
        {
            var conditionType = Next(ifExpression.Condition);
            CheckTypes(_boolType, conditionType, ifExpression.Condition.Span);
            var branchExprStmt = (ExpressionStatement)ifExpression.Branch;
            var branchBlock = (BlockExpression)branchExprStmt.Expression;
            var branchType = Next(branchBlock);
            ifExpression.DataType = branchType;

            if (ifExpression.ElseBranch is ExpressionStatement elseBranchExprStmt &&
                elseBranchExprStmt.Expression is BlockExpression elseBranchBlock)
            {
                var elseBranchType = Next(elseBranchBlock);
                CheckTypes(branchType, elseBranchType, ifExpression.Span);

                return branchType;
            }

            return _voidType;
        }

        public DataType Visit(SelfExpression selfExpression)
        {
            if (_current.CurrentObject?.DataType != null)
                return _current.CurrentObject.DataType;

            if (_current.CurrentExtendedType != null)
                return _current.CurrentExtendedType;

            _diagnostics.ReportMisplacedSelfKeyword(selfExpression.Span);

            return _unknownType;
        }

        private ModuleEnvironment? GetModule(List<Token> modulePath)
        {
            var module = _module.Parent!.FindByPath(modulePath);

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
                return variableDecl.DataType;

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
                var parameterType = parameter.Type!.DataType ?? Next(parameter.Type!);
                var argumentType = Next(argument, parameterType);
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