﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caique.Ast;
using Caique.CheckedTree;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Util;

namespace Caique.Semantics
{
    class TypeChecker : IAstTraverser<CheckedStatement, CheckedExpression>
    {
        private readonly ModuleEnvironment _module;
        private readonly DiagnosticBag _diagnostics;
        private SymbolEnvironment _environment;
        private TypeCheckerContext _current = new();
        private StructSymbol? _stringObj;
        private static readonly PrimitiveType _voidType = new(TypeKeyword.Void);
        private static readonly PrimitiveType _boolType = new(TypeKeyword.Bool);
        private static readonly PrimitiveType _unknownType = new(TypeKeyword.Unknown);

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

        private CheckedStatement Next(Statement statement)
        {
            _current = _current.CreateChild(statement);
            var value = ((IAstTraverser<CheckedStatement, CheckedExpression>)this).Next(statement);
            _current = _current.Parent!;

            return value;
        }

        private CheckedExpression Next(Expression expression, IDataType? expectedType = null)
        {
            _current = _current.CreateChild(expression);
            _current.ExpectedType = expectedType ?? _current.DataType;
            _current.DataType = _current.Parent!.DataType; // Expressions should carry on the infer-type
            var value = ((IAstTraverser<CheckedStatement, CheckedExpression>)this).Next(expression);
            _current.CheckedExpression = value;
            _current = _current.Parent!;

            return value;
        }

        public IEnumerable<CheckedStatement> Analyse()
        {
            foreach (var statement in _module.Ast!)
                yield return Next(statement);
        }

        public CheckedStatement Visit(ExpressionStatement expressionStatement)
        {
            return new CheckedExpressionStatement(Next(expressionStatement.Expression));
        }

        public CheckedStatement Visit(VariableDeclStatement variableDeclStatement)
        {
            _environment.TryAdd(variableDeclStatement, out VariableSymbol? symbol);

            return CheckVariableDecl(variableDeclStatement.Identifier, symbol)
                ?? (CheckedStatement)new CheckedUnknownStatement();
        }

        public CheckedStatement Visit(ReturnStatement returnStatement)
        {
            _current.DataType = _current.CurrentFunctionType;
            if (returnStatement.Expression == null)
            {
                return new CheckedReturnStatement(null);
            }

            var value = Next(returnStatement.Expression!, _current.CurrentFunctionType);
            CheckTypes(_current.CurrentFunctionType!, value.DataType, returnStatement.Span);

            return new CheckedReturnStatement(value);
        }

        public CheckedStatement Visit(AssignmentStatement assignmentStatement)
        {
            var assignee = Next(assignmentStatement.Assignee);
            _current.DataType = assignee.DataType;
            var value = Next(assignmentStatement.Value, assignee.DataType);

            CheckTypes(assignee.DataType, value.DataType, assignmentStatement.Span);

            // Go through the asignee and make sure it can be assigned to
            foreach (var expression in assignmentStatement.Assignee.Expressions)
            {
                if (!(expression is VariableExpression))
                {
                    _diagnostics.ReportMisplacedAssignmentOperator(assignmentStatement.Span);
                    break;
                }
            }

            return new CheckedAssignmentStatement(assignee, value);
        }

        public CheckedStatement Visit(FunctionDeclStatement functionDeclStatement)
        {
            _current.CurrentFunctionType = functionDeclStatement.ReturnType == null
                ? _voidType
                : Next(functionDeclStatement.ReturnType).DataType;
            _current.DataType = _current.CurrentFunctionType;

            if (functionDeclStatement.IsExtensionFunction)
            {
                _current.CurrentExtendedType = Next(functionDeclStatement.ExtensionOf!).DataType;
            }

            CheckedBlockExpression? body = null;
            if (functionDeclStatement.Body != null)
            {
                body = (CheckedBlockExpression)Next(functionDeclStatement.Body);
                CheckTypes(
                    _current.CurrentFunctionType!,
                    body.DataType,
                    functionDeclStatement.Body.Span
                );
            }

            var parameters = new List<CheckedVariableDeclStatement>();
            var previousEnvironment = _environment;
            _environment = functionDeclStatement.Body?.Environment ?? _environment;
            foreach (var uncheckedParameter in functionDeclStatement.Parameters)
            {
                parameters.Add((CheckedVariableDeclStatement)Next(uncheckedParameter));
            }

            _environment = previousEnvironment;

            var parentObject = _current.CurrentObject != null
                ? _module.GetClass(_current.CurrentObject!.Identifier.Value, false)!
                : null;
            var extensionOf = functionDeclStatement.IsExtensionFunction
                ? Next(functionDeclStatement.ExtensionOf!).DataType
                : null;

            var checkedFunction = new CheckedFunctionDeclStatement(
                functionDeclStatement.Identifier,
                parameters,
                body,
                _current.CurrentFunctionType,
                functionDeclStatement.IsInitFunction,
                parentObject,
                _module,
                extensionOf
            );

            if (functionDeclStatement.Identifier.Value != "init")
            {
                var identifier = functionDeclStatement.Identifier.Value;
                var symbolName = functionDeclStatement.IsExtensionFunction
                    ? $"{functionDeclStatement.ExtensionOf!.ModulePath[^1].Value}.{identifier}"
                    : identifier;
                var symbol = _environment.GetFunction(symbolName)!;
                symbol.Checked = checkedFunction;
            }

            return checkedFunction;
        }

        public CheckedStatement Visit(ClassDeclStatement classDeclStatement)
        {
            var symbol = _module.GetClass(classDeclStatement.Identifier.Value, false)!;
            if (symbol.Checked != null) return symbol.Checked;

            var checkedClass = new CheckedClassDeclStatement(
                classDeclStatement.Identifier,
                classDeclStatement.Body.Environment,
                _module,
                classDeclStatement.InheritedType != null
                    ? Next(classDeclStatement.InheritedType).DataType
                    : null
            );
            symbol.Checked = checkedClass;
            _current.CurrentObject = checkedClass;

            if (classDeclStatement.InheritedType != null)
            {
                var inheritedType = (CheckedTypeExpression)Next(classDeclStatement.InheritedType);
                var ancestor = ((StructType)inheritedType.DataType).StructDecl;

                if (ancestor.Identifier.Value == classDeclStatement.Identifier.Value)
                {
                    _diagnostics.ReportUnableToInherit(
                        ancestor.Identifier,
                        classDeclStatement.Identifier
                    );
                }
            }

            // Constructor
            if (classDeclStatement.InitFunction != null)
            {
                var initFunction = (CheckedFunctionDeclStatement)Next(classDeclStatement.InitFunction);
                checkedClass.InitFunction = initFunction;

                if (initFunction.Body == null)
                {
                    initFunction.Body = new CheckedBlockExpression(
                        new(),
                        new SymbolEnvironment(_environment),
                        _voidType,
                        false
                    );
                }

                foreach (var (uncheckedParameter, checkedParameter) in
                    classDeclStatement.InitFunction.Parameters.Zip(initFunction.Parameters))
                {
                    // Is reference parameter
                    if (uncheckedParameter.SpecifiedType == null)
                    {
                        var fieldSymbol = checkedClass.GetVariable(uncheckedParameter.Identifier.Value);
                        var fieldDecl = CheckVariableDecl(uncheckedParameter.Identifier, fieldSymbol);
                        if (fieldSymbol == null || fieldDecl == null)
                        {
                            _diagnostics.ReportSymbolDoesNotExist(uncheckedParameter.Identifier);
                        }
                        else
                        {
                            checkedParameter.DataType = fieldSymbol.Checked!.DataType;
                            initFunction.Body!.Statements.Insert(
                                0,
                                new CheckedAssignmentStatement(
                                    new CheckedVariableExpression(
                                        uncheckedParameter.Identifier,
                                        fieldDecl,
                                        fieldDecl.DataType
                                    ),
                                    new CheckedVariableExpression(
                                        uncheckedParameter.Identifier,
                                        checkedParameter,
                                        checkedParameter.DataType
                                    )
                                )
                            );
                        }
                    }
                }

                CheckTypes(
                    checkedClass.InitFunction.Body!.DataType,
                    _voidType,
                    classDeclStatement.InitFunction.Body!.Span
                );
            }

            checkedClass.Body = (CheckedBlockExpression)Next(classDeclStatement.Body);

            return checkedClass;
        }

        public CheckedStatement Visit(WhileStatement whileStatement)
        {
            var condition = Next(whileStatement.Condition);
            CheckTypes(
                condition.DataType,
                _boolType,
                whileStatement.Condition.Span
            );

            return new CheckedWhileStatement(
                condition,
                (CheckedBlockExpression)Next(whileStatement.Body)
            );
        }

        public CheckedStatement Visit(UseStatement useStatement)
        {
            var module = _module.Parent!.FindByPath(useStatement.ModulePath);

            if (module != null)
            {
                _module.ImportModule(module);
                if (_module.Root.Identifier == "prelude" && module.Identifier == "string")
                {
                    _stringObj = module.GetClass("String")!;
                }
            }
            else
            {
                _diagnostics.ReportInvalidModulePath(useStatement.ModulePath);
            }

            return null!;
        }

        public CheckedExpression Visit(UnaryExpression unaryExpression)
        {
            var value = Next(unaryExpression.Value);

            if (!value.DataType.IsNumber)
            {
                _diagnostics.ReportUnexpectedType(
                    value.DataType,
                    "number",
                    unaryExpression.Span
                );
            }

            return new CheckedUnaryExpression(unaryExpression.Operator, value);
        }

        public CheckedExpression Visit(BinaryExpression binaryExpression)
        {
            var left = Next(binaryExpression.Left);
            _current.DataType = left.DataType;
            var right = Next(binaryExpression.Right, left.DataType);
            CheckTypes(left.DataType, right.DataType, binaryExpression.Span);

            var type = binaryExpression.Operator.Kind.IsComparisonOperator()
                ? _boolType
                : left.DataType;

            return new CheckedBinaryExpression(
                left,
                binaryExpression.Operator,
                right,
                type
            );
        }

        public CheckedExpression Visit(DotExpression dotExpression)
        {
            var left = Next(dotExpression.Expressions.First());
            foreach (var uncheckedRight in dotExpression.Expressions.Skip(1))
            {
                if (uncheckedRight is CallExpression extensionFunctionCall)
                {
                    var identifier = extensionFunctionCall.ModulePath[^1];
                    var extensionFunctionSymbol = _module.GetFunction($"{left.DataType}.{identifier.Value}");
                    if (extensionFunctionSymbol != null &&
                        extensionFunctionSymbol.Syntax.IsExtensionFunction &&
                        left.DataType.IsCompatible(extensionFunctionSymbol.Checked!.ExtensionOf!))
                    {
                        var checkedCall = CheckCall(
                            identifier,
                            extensionFunctionSymbol,
                            extensionFunctionCall.Arguments,
                            left
                        );

                        if (checkedCall == null) return new CheckedUnknownExpression();

                        left = checkedCall;
                        continue;
                    }
                }

                // If it's not an object
                if (left.DataType is not StructType leftStructType)
                {
                    _diagnostics.ReportUnexpectedType(left.DataType, "object", uncheckedRight.Span);

                    return new CheckedUnknownExpression();
                }

                var classSymbol = leftStructType.StructDecl;

                // Can't continue if the class couldn't be found.
                // This will probably mostly happen due to other user-errors,
                // which will reported where relevant instead.
                if (classSymbol == null) return new CheckedUnknownExpression();

                // Check if the variable/function exists in the class
                if (uncheckedRight is CallExpression callExpression)
                {
                    var identifier = callExpression.ModulePath[^1];
                    var functionSymbol = classSymbol.GetFunction(identifier.Value);
                    var call = CheckCall(
                        identifier,
                        functionSymbol,
                        callExpression.Arguments,
                        left
                    );

                    if (call == null) return new CheckedUnknownExpression();

                    left = call;
                }
                else if (uncheckedRight is VariableExpression variableExpression)
                {
                    var identifier = variableExpression.Identifier;
                    var variableSymbol = classSymbol.GetVariable(identifier.Value);
                    var variableDecl = CheckVariableDecl(identifier, variableSymbol);
                    if (variableDecl == null) return new CheckedUnknownExpression();

                    left = new CheckedVariableExpression(
                        identifier,
                        variableDecl,
                        variableDecl.DataType,
                        left
                    );
                }
                else
                {
                    _diagnostics.ReportUnexpectedToken(
                        new Token(TokenKind.Identifier, uncheckedRight.GetType().Name, uncheckedRight.Span),
                        "call expression or variable expression"
                    );

                    return new CheckedUnknownExpression();
                }
            }

            return left;
        }

        public CheckedExpression Visit(LiteralExpression literalExpression)
        {
            IDataType type;
            if (literalExpression.Value.Kind == TokenKind.NumberLiteral)
            {
                bool isFloat = literalExpression.Value.Value.Contains(".");
                type = _current.ExpectedType ?? new PrimitiveType(
                    isFloat ? TypeKeyword.f32 : TypeKeyword.i32
                );
            }
            else if (literalExpression.Value.Kind == TokenKind.StringLiteral)
            {
                type = _current.ExpectedType?.Type == TypeKeyword.i8 && _current.ExpectedType.IsExplicitPointer
                    ? new PrimitiveType(TypeKeyword.i8, true)
                    : new StructType(TypeKeyword.Identifier, _stringObj!.Checked!);
            }
            else if (literalExpression.Value.Kind == TokenKind.CharLiteral)
            {
                type = new PrimitiveType(TypeKeyword.i8);
            }
            else
            {
                throw new NotImplementedException();
            }

            return new CheckedLiteralExpression(literalExpression.Value, type);
        }

        public CheckedExpression Visit(GroupExpression groupExpression)
        {
            return new CheckedGroupExpression(Next(groupExpression.Expression));
        }

        public CheckedExpression Visit(BlockExpression blockExpression)
        {
            var previousEnvironment = _environment;
            _environment = blockExpression.Environment;
            IDataType returnType = _voidType;
            bool returnsLast = false;
            var statements = new List<CheckedStatement>();

            foreach (var (statement, i) in blockExpression.Statements.WithIndex())
            {
                bool isLast = i == blockExpression.Statements.Count - 1;

                // If it's an expression statement and
                // it doesn't have a trailing semicolon, it should be returned.
                if (isLast &&
                    statement is ExpressionStatement expressionStatement &&
                    !expressionStatement.TrailingSemicolon)
                {
                    _current.DataType = _current.Parent!.DataType;
                    var checkedReturn = (CheckedExpressionStatement)Next(expressionStatement);
                    statements.Add(checkedReturn);
                    returnType = checkedReturn.Expression.DataType;
                    returnsLast = true;
                }
                else if (statement is ReturnStatement returnStatement)
                {
                    var checkedReturn = (CheckedReturnStatement)Next(returnStatement);
                    _current.DataType = checkedReturn.Expression?.DataType ?? _voidType;
                    statements.Add(checkedReturn);
                    break;
                }
                else
                {
                    statements.Add(Next(statement));
                }
            }

            _environment = previousEnvironment;

            return new CheckedBlockExpression(
                statements,
                blockExpression.Environment,
                returnType,
                returnsLast
            );
        }

        public CheckedExpression Visit(VariableExpression variableExpression)
        {
            var variableName = variableExpression.Identifier;
            var variableSymbol = _environment.GetVariable(variableName.Value);
            if (variableSymbol == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(variableName);

                return new CheckedUnknownExpression();
            }

            variableSymbol.Checked = CheckVariableDecl(variableName, variableSymbol);

            if (variableSymbol.Checked == null) return new CheckedUnknownExpression();

            return new CheckedVariableExpression(
                variableName,
                variableSymbol.Checked,
                variableSymbol.Checked.DataType
            );
        }

        public CheckedExpression Visit(CallExpression callExpression)
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

                if (module == null) return new CheckedUnknownExpression();
            }

            FunctionSymbol? functionSymbol;
            if (_environment.ParentObject != null && modulePath.Count == 1)
            {
                functionSymbol = _environment.ParentObject?.Checked?.GetFunction(lastIdentifier.Value) ??
                    module.GetFunction(lastIdentifier.Value, false);
            }
            else
            {
                functionSymbol = module.GetFunction(
                    lastIdentifier.Value,
                    modulePath.Count > 1 // Look in imports if a proper module path is specified
                );
            }

            var checkedCall = CheckCall(
                lastIdentifier,
                functionSymbol,
                callExpression.Arguments,
                functionSymbol!.Syntax.IsMethod
                    ? new CheckedKeywordValueExpression(TokenKind.Self, _current.CurrentObject!.DataType)
                    : null
            );

            return checkedCall ?? (CheckedExpression)new CheckedUnknownExpression();
        }

        public CheckedExpression Visit(NewExpression newExpression)
        {
            var type = (StructType)((CheckedTypeExpression)Next(newExpression.Type)).DataType;
            var classDecl = type.StructDecl;
            /*if (classDecl == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(newExpression.Type.ModulePath[^1]);

                return new CheckedUnknownExpression();
            }*/

            int argumentCount = newExpression.Arguments.Count;
            int parameterCount = classDecl.InitFunction?.Parameters.Count ?? 0;
            if (argumentCount != parameterCount)
            {
                _diagnostics.ReportWrongNumberOfArguments(
                    classDecl.Identifier,
                    argumentCount,
                    parameterCount
                );

                return new CheckedUnknownExpression();
            }

            var arguments = new List<CheckedExpression>();
            foreach (var (uncheckedArgument, i) in newExpression.Arguments.WithIndex())
            {
                var varDecl = classDecl.Environment.GetVariable(
                    classDecl.InitFunction!.Parameters[i].Identifier.Value
                );

                if (varDecl == null) continue;
                if (varDecl.Checked == null) Next(varDecl.Syntax);
                var argument = Next(uncheckedArgument, varDecl.Checked!.DataType);

                CheckTypes(varDecl.Checked.DataType, argument.DataType, uncheckedArgument.Span);
                arguments.Add(argument);
            }

            return new CheckedNewExpression(
                arguments,
                type
            );
        }

        public CheckedExpression Visit(TypeExpression typeExpression)
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
                    return new CheckedTypeExpression(
                        new PrimitiveType(keyword, typeExpression.IsExplicitPointer)
                    );
                }
            }

            var lastIdentifier = typeExpression.ModulePath[^1];
            var module = _module;
            if (typeExpression.ModulePath.Count > 1)
            {
                module = GetModule(typeExpression.ModulePath);
                if (module == null) return new CheckedUnknownExpression();
            }

            var classSymbol = module.GetClass(lastIdentifier.Value);
            if (classSymbol != null && classSymbol.Checked == null)
            {
                classSymbol.Checked = (CheckedClassDeclStatement)Next(classSymbol.Syntax);
            }

            if (classSymbol == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(lastIdentifier);

                return new CheckedUnknownExpression();
            }

            return new CheckedTypeExpression(
                new StructType(
                    TypeKeyword.Identifier,
                    classSymbol.Checked!,
                    typeExpression.IsExplicitPointer
                )
            );
        }

        public CheckedExpression Visit(IfExpression ifExpression)
        {
            var condition = Next(ifExpression.Condition);
            CheckTypes(_boolType, condition.DataType, ifExpression.Condition.Span);
            var branch = Next(ifExpression.Branch);
            CheckedBlockExpression? elseBranch = null;

            if (ifExpression.ElseBranch != null)
            {
                elseBranch = (CheckedBlockExpression)Next(ifExpression.ElseBranch);
                CheckTypes(branch.DataType, elseBranch.DataType, ifExpression.Span);
            }

            return new CheckedIfExpression(
                condition,
                (CheckedBlockExpression)branch,
                elseBranch
            );
        }

        public CheckedExpression Visit(KeywordValueExpression keywordValueExpression)
        {
            if (keywordValueExpression.Token.Kind == TokenKind.Self)
            {
                if (_current.CurrentObject != null)
                {
                    return new CheckedKeywordValueExpression(
                        keywordValueExpression.Token.Kind,
                        _current.CurrentObject!.DataType
                    );
                }

                if (_current.CurrentExtendedType != null)
                {
                    return new CheckedKeywordValueExpression(
                        keywordValueExpression.Token.Kind,
                        _current.CurrentExtendedType
                    );
                }

                _diagnostics.ReportMisplacedSelfKeyword(keywordValueExpression.Span);

                return new CheckedUnknownExpression();
            }
            else if (keywordValueExpression.Token.Kind == TokenKind.True ||
                     keywordValueExpression.Token.Kind == TokenKind.False)
            {
                return new CheckedKeywordValueExpression(
                    keywordValueExpression.Token.Kind,
                    _boolType
                );
            }

            throw new NotImplementedException();
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

        private CheckedVariableDeclStatement? CheckVariableDecl(Token identifier, VariableSymbol? variableSymbol)
        {
            if (variableSymbol == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(identifier);

                return null;
            }

            // If it has already been checked once before,
            // there's no need to do anything else, just return the existing value.
            if (variableSymbol.Checked != null)
                return variableSymbol.Checked;

            var variableDeclStatement = variableSymbol!.Syntax!;
            CheckedExpression? value = null;
            IDataType dataType;

            // If a type was specified
            if (variableDeclStatement.SpecifiedType != null)
            {
                var specifiedType = Next(variableDeclStatement.SpecifiedType).DataType;
                dataType = specifiedType;
                _current.DataType = specifiedType;

                // Make sure the value type match up with the specified type
                // if there is a value
                if (variableDeclStatement.Value != null)
                {
                    value = Next(variableDeclStatement.Value!, specifiedType);
                    CheckTypes(
                        specifiedType,
                        value.DataType,
                        variableDeclStatement.Value.Span
                    );
                }
            }
            else
            {
                if (variableDeclStatement.Value != null)
                    value = Next(variableDeclStatement.Value);

                dataType = value?.DataType ?? _unknownType;
            }

            if (variableDeclStatement.VariableType == VariableType.Local)
            {
                if (!variableSymbol.Environment.ContainsVariable(variableDeclStatement.Identifier.Value))
                    _diagnostics.ReportSymbolAlreadyExists(variableDeclStatement.Identifier);
            }

            var variableDecl = new CheckedVariableDeclStatement(
                variableDeclStatement.Identifier,
                value,
                dataType,
                variableDeclStatement.VariableType,
                variableDeclStatement.IndexInObject
            );
            variableSymbol.Checked = variableDecl;

            return variableDecl;
        }

        private CheckedCallExpression? CheckCall(Token identifier,
                                                 FunctionSymbol? functionSymbol,
                                                 List<Expression> arguments,
                                                 CheckedExpression? objectInstance = null)
        {
            if (functionSymbol == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(identifier);

                return null;
            }

            //var returnType = functionDecl.ReturnType ?? _voidType;

            // If wrong number of arguments
            if (arguments.Count != functionSymbol.Syntax.Parameters.Count)
            {
                _diagnostics.ReportWrongNumberOfArguments(
                    identifier,
                    arguments.Count,
                    functionSymbol.Syntax.Parameters.Count
                );

                return null;
            }

            var checkedArguments = new List<CheckedExpression>();
            foreach (var (argument, parameter) in
                     arguments.Zip(functionSymbol.Syntax.Parameters))
            {
                var parameterType = Next(parameter.SpecifiedType!).DataType;
                var checkedArgument = Next(argument, parameterType);
                checkedArguments.Add(checkedArgument);
                CheckTypes(parameterType, checkedArgument.DataType, argument.Span);
            }


            var returnTypeExpression = functionSymbol.Syntax.ReturnType;
            return new CheckedCallExpression(
                checkedArguments,
                functionSymbol,
                returnTypeExpression == null
                    ? _voidType
                    : Next(returnTypeExpression).DataType,
                objectInstance
            );
        }

        private bool CheckTypes(IDataType expected, IDataType got, TextSpan span)
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