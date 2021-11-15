using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caique.Ast;
using Caique.CheckedTree;
using Caique.Diagnostics;
using Caique.Parsing;
using System.Text;
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
        private static readonly PrimitiveType _isizeType = new(TypeKeyword.isize);
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

        public static List<CheckedStatement> Analyse(ModuleEnvironment module)
        {
            var typeChecker = new TypeChecker(module);
            var statements = new List<CheckedStatement>();
            foreach (var statement in typeChecker._module.Ast!)
                statements.Add(typeChecker.Next(statement));

            return statements;
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

            // Go through the assignee and make sure it can be assigned to
            foreach (var expression in assignmentStatement.Assignee.Expressions)
            {
                if (expression is not VariableExpression)
                {
                    _diagnostics.ReportMisplacedAssignmentOperator(assignmentStatement.Span);
                    break;
                }
            }

            return new CheckedAssignmentStatement(assignee, value);
        }

        public CheckedStatement Visit(FunctionDeclStatement functionDeclStatement)
        {
            // Same *exact* one has already been checked
            if (_current.CurrentCheckedClass != null)
            {
                var alreadyChecked = _current.CurrentCheckedClass.GetFunction(functionDeclStatement.FullName, false);
                if (alreadyChecked != null) return alreadyChecked;
            }
            else
            {
                var alreadyChecked = _module.GetFunction(functionDeclStatement.FullName)?.AllChecked.FirstOrDefault();
                if (alreadyChecked != null) return alreadyChecked;
            }

            if (functionDeclStatement.IsVirtual && !functionDeclStatement.IsMethod)
            {
                _diagnostics.ReportMisplacedVirtual(functionDeclStatement.Span);
            }

            if (functionDeclStatement.IsOverride && !functionDeclStatement.IsMethod)
            {
                _diagnostics.ReportMisplacedOverride(functionDeclStatement.Span);
            }

            var inheritedFunction = _current.CurrentCheckedClass?.Inherited?.GetFunction(functionDeclStatement.FullName);
            if (inheritedFunction != null)
            {
                if (!inheritedFunction.IsVirtual)
                {
                    _diagnostics.ReportCannotOverrideNonVirtual(functionDeclStatement.Span);
                }

                if (!functionDeclStatement.IsOverride)
                {
                    _diagnostics.ReportExpectedOverride(functionDeclStatement.Span);
                }
            }

            // Should only be null if it's an init function
            var symbol = _current.CurrentCheckedClass?.Environment.GetFunction(functionDeclStatement.FullName) ??
                _module.GetFunction(functionDeclStatement.FullName);
            if (symbol?.HasChecked ?? false)
            {
                // If it's from the same syntactical scope but belongs to
                // another class signature. For example for different
                // type arguments.
                var similarFunction = symbol.AllChecked.First();
                var newCheckedFunction = new CheckedFunctionDeclStatement(
                    functionDeclStatement.Identifier,
                    similarFunction.Parameters,
                    similarFunction.Body,
                    similarFunction.ReturnType,
                    functionDeclStatement.IsInitFunction,
                    functionDeclStatement.IsVirtual,
                    functionDeclStatement.IsOverride,
                    _current.CurrentCheckedClass,
                    _module,
                    similarFunction.ExtensionOf
                );

                if (!functionDeclStatement.IsInitFunction)
                {
                    symbol!.AddChecked(newCheckedFunction);
                }

                if (functionDeclStatement.IsVirtual && _current.CurrentCheckedClass != null)
                {
                    _current.CurrentCheckedClass.RegisterVirtualMethod(newCheckedFunction);
                }

                if (functionDeclStatement.IsOverride)
                {
                    inheritedFunction?.RegisterOverride(newCheckedFunction);
                }

                return newCheckedFunction;
            }

            _current.CurrentFunctionType = functionDeclStatement.ReturnType == null
                ? _voidType
                : Next(functionDeclStatement.ReturnType).DataType;
            _current.DataType = _current.CurrentFunctionType;
            _current.CurrentFunctionDecl = functionDeclStatement;

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
                // If there is no body, just create the object,
                // since there is no scope to define the variable in.
                CheckedVariableDeclStatement checkedParameter;
                if (functionDeclStatement.Body == null)
                {
                    checkedParameter = new CheckedVariableDeclStatement(
                        uncheckedParameter.Identifier,
                        null,
                        Next(uncheckedParameter.SpecifiedType!).DataType,
                        VariableType.FunctionParameter
                    );
                }
                else
                {
                    checkedParameter = (CheckedVariableDeclStatement)Next(uncheckedParameter);
                }

                parameters.Add(checkedParameter);
            }

            _environment = previousEnvironment;
            _current.CurrentFunctionDecl = null;

            var extensionOf = functionDeclStatement.IsExtensionFunction
                ? Next(functionDeclStatement.ExtensionOf!).DataType
                : null;

            var checkedFunction = new CheckedFunctionDeclStatement(
                functionDeclStatement.Identifier,
                parameters,
                body,
                _current.CurrentFunctionType,
                functionDeclStatement.IsInitFunction,
                functionDeclStatement.IsVirtual,
                functionDeclStatement.IsOverride,
                _current.CurrentCheckedClass,
                _module,
                extensionOf
            );

            if (!functionDeclStatement.IsInitFunction)
            {
                symbol!.AddChecked(checkedFunction);
            }

            if (functionDeclStatement.IsVirtual && _current.CurrentCheckedClass != null)
            {
                _current.CurrentCheckedClass.RegisterVirtualMethod(checkedFunction);
            }

            if (functionDeclStatement.IsOverride)
            {
                inheritedFunction?.RegisterOverride(checkedFunction);
            }

            return checkedFunction;
        }

        public CheckedStatement Visit(ClassDeclStatement classDeclStatement)
        {
            var symbol = _module.GetClass(classDeclStatement.Identifier.Value, false)!;
            var alreadyChecked = _current.TypeArgumentsForClass == null
                ? symbol.GetChecked(classDeclStatement.Identifier.Value)
                : symbol.GetCheckedFromTypeArguments(_current.TypeArgumentsForClass);
            if (alreadyChecked != null) return alreadyChecked;

            var checkedClass = new CheckedClassDeclStatement(
                classDeclStatement.Identifier,
                _current.TypeArgumentsForClass,
                classDeclStatement.Body.Environment,
                _module,
                classDeclStatement.InheritedType != null
                    ? (StructType)Next(classDeclStatement.InheritedType).DataType
                    : null
            );
            symbol.AddChecked(checkedClass);
            _current.CurrentCheckedClass = checkedClass;

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

            var previousClassDecl = _current.CurrentClassDecl;
            _current.CurrentClassDecl = classDeclStatement;

            // Constructor
            if (classDeclStatement.InitFunction != null)
            {
                var initFunction = (CheckedFunctionDeclStatement)Next(classDeclStatement.InitFunction);
                checkedClass.InitFunction = initFunction;

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
            else
            {
                // Create an empty init functions to allow for eg.
                // field assignments later on.
                checkedClass.InitFunction = new CheckedFunctionDeclStatement(
                    new Token(TokenKind.Identifier, "init", new(new(0, 0), new(0, 0))),
                    new(),
                    new CheckedBlockExpression(
                        new(),
                        _environment.CreateChildEnvironment(),
                        new PrimitiveType(TypeKeyword.Void),
                        false
                    ),
                    new PrimitiveType(TypeKeyword.Void),
                    true,
                    false,
                    false,
                    checkedClass,
                    _module
                );
            }

            checkedClass.Body = (CheckedBlockExpression)Next(classDeclStatement.Body);
            _current.CurrentClassDecl = previousClassDecl;

            // Add super() to the top of the constructor
            if (checkedClass.Inherited != null)
                InsertSuper(checkedClass, classDeclStatement.Span);

            // Add assignment statements for the default field values
            var fieldAssignments = new List<CheckedAssignmentStatement>();
            foreach (var field in checkedClass.Environment.Variables)
            {
                if (field == null || field.Checked?.Value == null) continue;
                fieldAssignments.Add(
                    new CheckedAssignmentStatement(
                        new CheckedVariableExpression(
                            field.Syntax.Identifier,
                            field.Checked,
                            field.Checked.DataType
                        ),
                        field.Checked.Value
                    )
                );
            }

            checkedClass.InitFunction!.Body!.Statements.InsertRange(0, fieldAssignments);

            return checkedClass;
        }

        public void InsertSuper(CheckedClassDeclStatement checkedClass, TextSpan span)
        {
            if (checkedClass.Inherited == null) return;

            var firstInitStatement = checkedClass.InitFunction!.Body?.Statements.FirstOrDefault();
            bool firstStatementIsSuper = firstInitStatement is CheckedExpressionStatement expressionStatement &&
                expressionStatement.Expression is CheckedCallExpression call &&
                call.FunctionDecl == checkedClass.Inherited.InitFunction;
            if (!firstStatementIsSuper)
            {
                if (checkedClass.Inherited.InitFunction!.Parameters.Count > 0)
                {
                    _diagnostics.ReportExpectedSuper(span);
                }

                var superCall = new CheckedCallExpression(
                    new(),
                    checkedClass.Inherited.InitFunction!,
                    checkedClass.Inherited.InitFunction!.ReturnType,
                    new CheckedKeywordValueExpression(TokenKind.Self, null, checkedClass.DataType)
                );
                checkedClass.InitFunction.Body!.Statements.Insert(
                    0,
                    new CheckedExpressionStatement(superCall)
                );
            }
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
                    var checkedExtensionFunction = extensionFunctionSymbol?.AllChecked.First();
                    if (extensionFunctionSymbol != null &&
                        extensionFunctionSymbol.Syntax.IsExtensionFunction &&
                        left.DataType.IsCompatible(checkedExtensionFunction!.ExtensionOf!))
                    {
                        var checkedCall = CheckCall(
                            identifier,
                            checkedExtensionFunction,
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
                    if (left.DataType.Type != TypeKeyword.Unknown)
                    {
                        _diagnostics.ReportUnexpectedType(left.DataType, "object", uncheckedRight.Span);
                    }

                    return new CheckedUnknownExpression();
                }

                var checkedClass = leftStructType.StructDecl;

                // Can't continue if the class couldn't be found.
                // This will probably mostly happen due to other user-errors,
                // which will reported where relevant instead.
                if (checkedClass == null) return new CheckedUnknownExpression();

                // Check if the variable/function exists in the class
                if (uncheckedRight is CallExpression callExpression)
                {
                    var identifier = callExpression.ModulePath[^1];
                    var checkedFunction = checkedClass.GetFunction(identifier.Value);
                    if (checkedFunction == null)
                    {
                        var symbol = checkedClass.Environment.GetFunction(identifier.Value);
                        checkedFunction = (CheckedFunctionDeclStatement)Next(symbol!.Syntax);
                    }

                    var call = CheckCall(
                        identifier,
                        checkedFunction,
                        callExpression.Arguments,
                        left
                    );

                    if (call == null) return new CheckedUnknownExpression();

                    left = call;
                }
                else if (uncheckedRight is VariableExpression variableExpression)
                {
                    var identifier = variableExpression.Identifier;
                    var checkedVariable = checkedClass.GetVariable(identifier.Value);
                    var variableDecl = CheckVariableDecl(identifier, checkedVariable);
                    if (variableDecl == null) return new CheckedUnknownExpression();

                    var dataType = checkedVariable!.Checked!.DataType;
                    if (dataType is GenericType dataTypeGeneric &&
                        left?.DataType is StructType objectInstanceType)
                    {
                        dataType = objectInstanceType.TypeArguments![dataTypeGeneric.ParameterIndex];
                    }

                    left = new CheckedVariableExpression(
                        identifier,
                        variableDecl,
                        dataType,
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
                    : new StructType(TypeKeyword.Identifier, null, _stringObj!.AllChecked.First());
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

            CheckedFunctionDeclStatement? checkedFunction;
            FunctionSymbol? symbol;
            if (_current.CurrentCheckedClass != null && modulePath.Count == 1)
            {
                // If it's not in a class, it won't have different semantical variations (yet),
                // so it's safe to just take the first (and only) checked one from the symbol.
                var parentClass = _current.CurrentCheckedClass.GetParentClassForFunction(lastIdentifier.Value);
                if (parentClass != null)
                {
                    checkedFunction = parentClass.GetFunction(lastIdentifier.Value);
                    symbol = parentClass.Environment.GetFunction(lastIdentifier.Value);
                }
                else
                {
                    symbol = module.GetFunction(lastIdentifier.Value, false);
                    checkedFunction = symbol?.AllChecked.FirstOrDefault();
                }
            }
            else
            {
                // If it's not in a class, it won't have different semantical variations (yet),
                // so it's safe to just take the first (and only) checked one from the symbol.
                symbol = module.GetFunction(
                    lastIdentifier.Value,
                    modulePath.Count > 1 // Look in imports if a proper module path is specified
                );
                checkedFunction = symbol?.AllChecked.FirstOrDefault();
            }

            if (symbol == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(lastIdentifier);

                return new CheckedUnknownExpression();
            }

            if (checkedFunction == null)
            {
                var previousObject = _current.CurrentCheckedClass;
                if (!symbol.Syntax.IsMethod) _current.CurrentCheckedClass = null;
                checkedFunction = (CheckedFunctionDeclStatement)Next(symbol!.Syntax);
                _current.CurrentCheckedClass = previousObject;
            }

            var checkedCall = CheckCall(
                lastIdentifier,
                checkedFunction,
                callExpression.Arguments,
                checkedFunction!.IsMethod
                    ? new CheckedKeywordValueExpression(TokenKind.Self, null, _current.CurrentCheckedClass!.DataType)
                    : null
            );

            return checkedCall ?? (CheckedExpression)new CheckedUnknownExpression();
        }

        public CheckedExpression Visit(NewExpression newExpression)
        {
            var type = (StructType)((CheckedTypeExpression)Next(newExpression.Type)).DataType;
            var classDecl = type.StructDecl;

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

                CheckedExpression argument;
                if (varDecl == null)
                {
                    var parameterType = classDecl.InitFunction?.Parameters[i].DataType;
                    argument = Next(uncheckedArgument, parameterType);
                    CheckTypes(parameterType!, argument.DataType, uncheckedArgument.Span);
                }
                else
                {
                    if (varDecl.Checked == null) Next(varDecl.Syntax);
                    var parameterType = varDecl.Checked!.DataType;
                    if (varDecl.Checked!.DataType is GenericType genericType)
                    {
                        parameterType = type.TypeArguments![genericType.ParameterIndex];
                    }

                    argument = Next(uncheckedArgument, parameterType);
                    CheckTypes(parameterType, argument.DataType, uncheckedArgument.Span);
                }

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
                    TokenKind.isize => TypeKeyword.isize,
                    TokenKind.f8 => TypeKeyword.f8,
                    TokenKind.f32 => TypeKeyword.f32,
                    TokenKind.f64 => TypeKeyword.f64,
                    TokenKind.Bool => TypeKeyword.Bool,
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

            // Check type arguments for the type itself
            List<IDataType>? typeArguments = null;
            if (typeExpression.TypeArguments != null)
            {
                typeArguments = new List<IDataType>();
                foreach (var argument in typeExpression.TypeArguments)
                {
                    typeArguments.Add(Next(argument).DataType);
                }
            }

            // Find out if the type itself is a type argument
            var objectTypeParameters = _current.CurrentClassDecl?.TypeParameters;
            if (typeExpression.ModulePath.Count == 1 && objectTypeParameters != null)
            {
                for (int i = 0; i < objectTypeParameters.Count; i ++)
                {
                    if (objectTypeParameters[i].Value == lastIdentifier.Value)
                    {
                        return new CheckedTypeExpression(
                            new GenericType(lastIdentifier, i, typeExpression.IsExplicitPointer)
                        );
                    }
                }
            }

            var classSymbol = module.GetClass(lastIdentifier.Value);
            var checkedClass = typeArguments == null
                ? classSymbol?.GetChecked(lastIdentifier.Value)
                : classSymbol?.GetCheckedFromTypeArguments(typeArguments);
            if (classSymbol != null && checkedClass == null)
            {
                _current.TypeArgumentsForClass = typeArguments;
                checkedClass = (CheckedClassDeclStatement)Next(classSymbol.Syntax);
                _current.TypeArgumentsForClass = null;
            }

            if (classSymbol == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(lastIdentifier);

                return new CheckedUnknownExpression();
            }

            return new CheckedTypeExpression(
                new StructType(
                    TypeKeyword.Identifier,
                    typeArguments,
                    checkedClass!,
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
            var kind = keywordValueExpression.Token.Kind;
            if (kind == TokenKind.Self)
            {
                if (_current.CurrentCheckedClass != null)
                {
                    return new CheckedKeywordValueExpression(
                        kind,
                        null,
                        _current.CurrentCheckedClass!.DataType
                    );
                }

                if (_current.CurrentExtendedType != null)
                {
                    return new CheckedKeywordValueExpression(
                        kind,
                        null,
                        _current.CurrentExtendedType
                    );
                }

                _diagnostics.ReportMisplacedSelfKeyword(keywordValueExpression.Span);

                return new CheckedUnknownExpression();
            }
            else if (kind == TokenKind.Super)
            {
                if (keywordValueExpression.Arguments != null && keywordValueExpression.Arguments.Count > 0)
                {
                    if (!_current.CurrentFunctionDecl?.IsInitFunction ?? true)
                    {
                        _diagnostics.ReportMisplacedSuperKeywordWithArguments(keywordValueExpression.Token.Span);

                        return new CheckedUnknownExpression();
                    }

                    if (_current.CurrentCheckedClass?.Inherited?.InitFunction == null)
                    {
                        _diagnostics.ReportMisplacedSuperKeywordWithArguments(keywordValueExpression.Token.Span);

                        return new CheckedUnknownExpression();
                    }

                    return CheckCall(
                        keywordValueExpression.Token,
                        _current.CurrentCheckedClass.Inherited.InitFunction,
                        keywordValueExpression.Arguments,
                        new CheckedKeywordValueExpression(
                            TokenKind.Self,
                            null,
                            _current.CurrentCheckedClass.DataType
                        )
                    )!;
                }
            }
            else if (kind == TokenKind.Sizeof)
            {
                if (keywordValueExpression.Arguments?.Count != 1)
                {
                    _diagnostics.ReportWrongNumberOfArguments(
                        keywordValueExpression.Token,
                        keywordValueExpression.Arguments?.Count ?? 0,
                        1
                    );

                    // Return 0
                    return new CheckedLiteralExpression(
                        new Token(TokenKind.NumberLiteral, "0", new(new(0, 0), new(0, 0))),
                        _isizeType
                    );
                }

                return new CheckedKeywordValueExpression(
                    kind,
                    new() { Next(keywordValueExpression.Arguments![0]) },
                    _isizeType
                );
            }
            else if (kind == TokenKind.True ||
                     kind == TokenKind.False)
            {
                return new CheckedKeywordValueExpression(
                    kind,
                    null,
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
                                                 CheckedFunctionDeclStatement? checkedFunction,
                                                 List<Expression> arguments,
                                                 CheckedExpression? objectInstance = null)
        {
            if (checkedFunction == null)
            {
                _diagnostics.ReportSymbolDoesNotExist(identifier);

                return null;
            }

            // If wrong number of arguments
            if (arguments.Count != checkedFunction.Parameters.Count)
            {
                _diagnostics.ReportWrongNumberOfArguments(
                    identifier,
                    arguments.Count,
                    checkedFunction.Parameters.Count
                );

                return null;
            }

            var checkedArguments = new List<CheckedExpression>();
            foreach (var (argument, parameter) in
                     arguments.Zip(checkedFunction.Parameters))
            {
                var parameterType = parameter.DataType;
                if (parameter.DataType is GenericType parameterTypeGeneric &&
                    objectInstance?.DataType is StructType objectType)
                {
                    parameterType = objectType.TypeArguments![parameterTypeGeneric.ParameterIndex];
                }

                var checkedArgument = Next(argument, parameterType);
                checkedArguments.Add(checkedArgument);
                CheckTypes(parameterType, checkedArgument.DataType, argument.Span);
            }

            var returnType = checkedFunction.ReturnType;
            if (returnType is GenericType returnTypeGeneric &&
                objectInstance?.DataType is StructType objectInstanceType)
            {
                returnType = objectInstanceType.TypeArguments![returnTypeGeneric.ParameterIndex];
            }

            return new CheckedCallExpression(
                checkedArguments,
                checkedFunction,
                returnType,
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