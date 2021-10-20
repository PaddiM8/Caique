using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Caique.Ast;
using Caique.CheckedTree;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public unsafe class LlvmGenerator : ICheckedTreeTraverser<LLVMValueRef, LLVMValueRef>, IDisposable
    {
        private readonly ModuleEnvironment _module;
        private readonly LLVMContextRef _context;
        private readonly LLVMModuleRef _llvmModule;
        private readonly LLVMBuilderRef _builder;
        private LlvmGeneratorContext _current = new();

        public LlvmGenerator(ModuleEnvironment module)
        {
            _module = module;
            _context = LLVM.ContextCreate();
            _llvmModule = LLVM.ModuleCreateWithName(
                _module.Identifier.ToCString()
            );
            _builder = LLVM.CreateBuilderInContext(_context);
        }

        public void Generate()
        {
            var functions = new List<CheckedFunctionDeclStatement>();
            foreach (var statement in _module.TypeTree!)
            {
                switch (statement)
                {
                    case CheckedClassDeclStatement classDeclStatement:
                        // May already have been done by TypeExpression.
                        if (classDeclStatement.LlvmType == null)
                            Next(statement);

                        if (classDeclStatement.InitFunction != null)
                        {
                            if (classDeclStatement.InitFunction.LlvmValue == null)
                                Next(classDeclStatement.InitFunction);

                            functions.Add(classDeclStatement.InitFunction);
                        }

                        foreach (var function in classDeclStatement.Body!.Environment.Functions)
                        {
                            Next(function.Checked!);
                            if (function.Checked!.Body != null) functions.Add(function.Checked);
                        }
                        break;
                    case CheckedFunctionDeclStatement functionDeclStatement:
                        Next(statement);

                        if (functionDeclStatement.Body != null)
                            functions.Add(functionDeclStatement);
                        break;
                }
            }

            foreach (var functionDeclStatement in functions)
            {
                _current = new LlvmGeneratorContext
                {
                    Parent = null,
                    Statement = functionDeclStatement
                };

                _current.FunctionDecl = functionDeclStatement;
                Next(functionDeclStatement.Body!);
            }

            sbyte* moduleError;
            _ = LLVM.VerifyModule(
                _llvmModule,
                LLVMVerifierFailureAction.LLVMPrintMessageAction,
                &moduleError
            );
            if (moduleError != null) Console.WriteLine(*moduleError);

            // Print out the LLVM IR
            LLVM.DumpModule(_llvmModule);
            int _2 = LLVM.PrintModuleToFile(_llvmModule, ("experiments/ll/" + _module.Identifier + ".ll").ToCString(), &moduleError);
        }

        public void Dispose()
        {
            LLVM.ContextDispose(_context);
            GC.SuppressFinalize(this);
        }

        public void GenerateObjectFile(string targetDirectory, bool isLinked = false)
        {
            LLVM.InitializeNativeTarget();
            LLVM.InitializeNativeAsmPrinter();

            sbyte* errorMessage;
            LLVMTarget* target;
            int _ = LLVM.GetTargetFromTriple(
                LLVM.GetDefaultTargetTriple(),
                &target,
                &errorMessage
            );

            var targetMachine = LLVM.CreateTargetMachine(
                target,
                LLVM.GetDefaultTargetTriple(),
                LLVM.GetHostCPUName(),
                LLVM.GetHostCPUFeatures(),
                LLVMCodeGenOptLevel.LLVMCodeGenLevelNone,
                LLVMRelocMode.LLVMRelocDefault,
                LLVMCodeModel.LLVMCodeModelDefault
            );

            string name = isLinked
                ? "linked"
                : _module.Identifier;

            _ = LLVM.TargetMachineEmitToFile(
                targetMachine,
                _llvmModule,
                (targetDirectory + "/" + name + ".o").ToCString(),
                LLVMCodeGenFileType.LLVMObjectFile,
                &errorMessage
            );
        }

        private LLVMValueRef Next(CheckedStatement statement)
        {
            _current = _current.CreateChild(statement);
            var value = ((ICheckedTreeTraverser<LLVMValueRef, LLVMValueRef>)this).Next(statement);
            _current = _current.Parent!;

            return value;
        }

        private LLVMValueRef Next(CheckedExpression expression)
        {
            _current = _current.CreateChild(expression);
            var value = ((ICheckedTreeTraverser<LLVMValueRef, LLVMValueRef>)this).Next(expression);
            _current = _current.Parent!;

            return value;
        }


        public LLVMValueRef Visit(CheckedExpressionStatement expressionStatement)
        {
            Next(expressionStatement.Expression);

            return null!;
        }

        public LLVMValueRef Visit(CheckedVariableDeclStatement variableDeclStatement)
        {
            // Hmm...
            //if (variableDeclStatement.SpecifiedType != null)
            //    Next(variableDeclStatement.SpecifiedType);

            string identifier = variableDeclStatement.Identifier.Value;
            LLVMValueRef initializer = variableDeclStatement.Value == null
                ? null
                : Next(variableDeclStatement.Value);
            LLVMTypeRef type = variableDeclStatement.DataType!.ToLlvmType(_module.Prelude);

            // Allocate variable
            var firstBlockValue = _current.Block!.Statements.First().LlvmValue;
            if (firstBlockValue != null)
            {
                // Add allocas to the top
                LLVM.PositionBuilderBefore(_builder, firstBlockValue!.Value);
            }

            LLVMValueRef alloca = LLVM.BuildAlloca(
                _builder,
                type,
                identifier.ToCString()
            );

            // Put the builder back where it should be
            DeselectTemporaryBlock();

            variableDeclStatement.LlvmValue = alloca;

            if (variableDeclStatement.Value != null)
            {
                LLVM.BuildStore(_builder, initializer, alloca);
            }

            return null!;
        }

        public LLVMValueRef Visit(CheckedReturnStatement returnStatement)
        {
            if (returnStatement.Expression == null)
            {
                LLVM.BuildRetVoid(_builder);

                return null!;
            }

            var value = Next(returnStatement.Expression);

            if (returnStatement.Expression.DataType is StructType)
                ArcRetain(value);

            // Arc release the values that should be released,
            // since the block won't do it automatically if you don't
            // reach the end of the scope.
            foreach (var valueToRelease in _current.Block!.ValuesToArcUpdate)
            {
                ArcRelease(valueToRelease);
            }

            LLVM.BuildRet(
                _builder,
                value
            );

            return null!;
        }

        public LLVMValueRef Visit(CheckedAssignmentStatement assignmentStatement)
        {
            // ARC release the previous reference
            if (assignmentStatement.Assignee.DataType is StructType)
                ArcRelease(LLVM.BuildLoad(_builder, assignmentStatement.Assignee.LlvmValue!.Value, "".ToCString()));

            _current.ShouldBeLoaded = false;
            LLVMValueRef assignee = Next(assignmentStatement.Assignee);
            _current.ShouldBeLoaded = true;
            LLVMValueRef value = Next(assignmentStatement.Value);
            LLVM.BuildStore(_builder, value, assignee);

            // ARC increment the new reference
            if (assignmentStatement.Assignee.DataType is StructType)
                ArcRetain(LLVM.BuildLoad(_builder, assignee, "".ToCString()));

            return null!;
        }

        public LLVMValueRef Visit(CheckedFunctionDeclStatement functionDeclStatement)
        {
            if (_module.Identifier == "main")
            {
                Console.WriteLine("main");
            }
            string identifier = functionDeclStatement.FullName;
            var parameterDataTypes = new List<IDataType>();

            // If it belongs to an object
            int parameterOffset = 0;
            if (functionDeclStatement.ParentObject != null)
            {
                parameterOffset++;

                // Add the parent object type as the first parameter
                parameterDataTypes.Add(functionDeclStatement.ParentObject.Checked!.DataType!);
            }

            if (functionDeclStatement.IsExtensionFunction)
            {
                identifier = identifier + "." + functionDeclStatement.ExtensionOf;
                parameterOffset++;

                // Add the extended type as the first parameter
                parameterDataTypes.Add(functionDeclStatement.ExtensionOf!);
            }

            // Add the parameter types to the parameter list
            parameterDataTypes.AddRange(
                functionDeclStatement.Parameters.Select(x => x.DataType!)
            );

            LLVMTypeRef functionType = LLVM.FunctionType(
                functionDeclStatement.ReturnType.ToLlvmType(_module.Prelude),
                parameterDataTypes.ToLlvmTypeArray(_module.Prelude),
                (uint)parameterDataTypes.Count,
                0
            );
            LLVMValueRef function = LLVM.AddFunction(
                _llvmModule,
                identifier.ToCString(),
                functionType
            );
            functionDeclStatement.LlvmValue = function;
            functionDeclStatement.LlvmType = functionType;

            // If the function doesn't have a body,
            // or a call/new expression triggered this function,
            // it just needs the declaration, not the body.
            if (functionDeclStatement.Body == null ||
                _current.Parent?.Expression is CheckedCallExpression ||
                _current.Parent?.Expression is CheckedTypeExpression)
            {
                return function;
            }

            LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                function,
                "functionEntry".ToCString()
            );
            TemporarilySetCurrentBlock(block);
            functionDeclStatement.BlockLlvmValue = block;

            // Position the builder where it should be again.
            if (_current.Block != null) DeselectTemporaryBlock();

            return function;
        }

        public LLVMValueRef Visit(CheckedClassDeclStatement classDeclStatement)
        {
            // Create the struct type
            string identifier = classDeclStatement.FullName;
            var namedStruct = LLVM.StructCreateNamed(
                _context,
                ("class." + identifier).ToCString()
            );

            classDeclStatement.LlvmType = namedStruct;

            // Set the struct field types
            var variableDecls = classDeclStatement.Body!.Environment.Variables;
            var fieldTypes = new List<IDataType>();

            CheckedClassDeclStatement? ancestor = classDeclStatement.Inherited;
            while (ancestor != null)
            {
                fieldTypes.AddRange(
                    ancestor.Body!.Environment.Variables.Select(x => x!.Checked!.DataType!)
                );

                ancestor = ancestor.Inherited;
            }

            int inheritedFieldCount = fieldTypes.Count;
            foreach (var variableSymbol in variableDecls)
            {
                var variableDecl = variableSymbol!.Checked;
                fieldTypes.Add(variableDecl!.DataType);

                // The first items in the struct will be the inherited ones,
                // so the IndexInObject value needs to be offset.
                variableDecl.IndexInObject += inheritedFieldCount;
            }

            LLVM.StructSetBody(
                namedStruct,
                fieldTypes.ToLlvmTypeArray(_module.Prelude),
                (uint)fieldTypes.Count,
                0
            );

            if (classDeclStatement.InitFunction != null &&
                _current.Parent?.Expression is not CheckedTypeExpression)
            {
                Next(classDeclStatement.InitFunction);
            }

            return null!;
        }

        public LLVMValueRef Visit(CheckedWhileStatement whileStatement)
        {
            var basicBlockParent = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));

            // Blocks
            LLVMBasicBlockRef conditionBasicBlock = LLVM.AppendBasicBlock(basicBlockParent, "cond".ToCString());
            LLVMBasicBlockRef branchBasicBlock = LLVM.AppendBasicBlock(basicBlockParent, "branch".ToCString());
            LLVMBasicBlockRef mergeBasicBlock = LLVM.AppendBasicBlock(basicBlockParent, "cont".ToCString());

            // Build condition
            LLVM.BuildBr(_builder, conditionBasicBlock);
            SetCurrentBlock(conditionBasicBlock);
            var condition = Next(whileStatement.Condition);
            LLVM.BuildCondBr(_builder, condition, branchBasicBlock, mergeBasicBlock);

            // Body
            whileStatement.Body.LlvmValue = branchBasicBlock;
            SetCurrentBlock(branchBasicBlock);
            Next(whileStatement.Body);
            LLVM.BuildBr(_builder, conditionBasicBlock);

            SetCurrentBlock(mergeBasicBlock);

            return null!;
        }

        public LLVMValueRef Visit(CheckedUseStatement useStatement)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(CheckedUnaryExpression unaryExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(CheckedBinaryExpression binaryExpression)
        {
            bool isFloat = binaryExpression.DataType!.IsFloat;
            LLVMValueRef leftValue = Next(binaryExpression.Left);
            LLVMValueRef rightValue = Next(binaryExpression.Right);
            if (binaryExpression.Operator.Kind.IsComparisonOperator())
            {
                if (isFloat)
                {
                    return LLVM.BuildFCmp(
                        _builder,
                        binaryExpression.Operator.Kind.ToLlvmRealPredicate(),
                        leftValue,
                        rightValue,
                        "cmp".ToCString()
                    );
                }
                else
                {
                    return LLVM.BuildICmp(
                        _builder,
                        binaryExpression.Operator.Kind.ToLlvmIntPredicate(),
                        leftValue,
                        rightValue,
                        "cmp".ToCString()
                    );
                }
            }

            LLVMOpcode opcode = binaryExpression.Operator.Kind.ToLlvmOpcode(
                isFloat
            );

            return LLVM.BuildBinOp(
                _builder,
                opcode,
                leftValue,
                rightValue,
                "binOp".ToCString()
            );
        }

        public LLVMValueRef Visit(CheckedLiteralExpression literalExpression)
        {
            var dataType = literalExpression.DataType!;
            if (dataType.IsNumber && !dataType.IsExplicitPointer)
            {
                string tokenValue = literalExpression.Value.Value;

                if (literalExpression.Value.Kind == TokenKind.CharLiteral)
                {
                    return LLVM.ConstInt(
                        dataType.ToLlvmType(_module.Prelude),
                        (ulong)tokenValue[0],
                        1
                    );
                }

                // Float
                if (dataType.IsFloat)
                {
                    return LLVM.ConstReal(
                        dataType.ToLlvmType(_module.Prelude),
                        double.Parse(tokenValue)
                    );
                }
                else // Int
                {
                    ulong value = ulong.Parse(tokenValue);
                    return LLVM.ConstInt(
                        dataType.ToLlvmType(_module.Prelude),
                        value,
                        value < 0 ? 0 : 1
                    );
                }
            }
            else if (dataType.Type == TypeKeyword.i8 ||
                     dataType is StructType type && type.StructDecl.FullName == "String")
            {
                LLVMValueRef globalString = LLVM.BuildGlobalString(
                    _builder,
                    literalExpression.Value.Value.ToCString(),
                    ".str".ToCString()
                );

                var indices = new LLVMOpaqueValue*[]
                {
                    LLVM.ConstInt(LLVM.Int64Type(), 0, 0),
                    LLVM.ConstInt(LLVM.Int64Type(), 0, 0),
                };

                fixed (LLVMOpaqueValue** indicesRef = indices)
                {
                    var stringPtr = LLVM.BuildGEP(
                        _builder,
                        globalString,
                        indicesRef,
                        2,
                        "str".ToCString()
                    );

                    if (literalExpression.DataType!.Type == TypeKeyword.i8)
                    {
                        return stringPtr;
                    }

                    var structType = (StructType)dataType;
                    var malloc = LLVM.BuildMalloc(
                        _builder,
                        structType.StructDecl.LlvmType!.Value,
                        "newString".ToCString()
                    );

                    var length = LLVM.ConstInt(
                        LLVM.Int32Type(),
                        (ulong)literalExpression.Value.Value.Length,
                        1
                    );

                    fixed (LLVMOpaqueValue** arguments = new LLVMOpaqueValue*[3])
                    {
                        arguments[0] = malloc;
                        arguments[1] = stringPtr;
                        arguments[2] = length;
                        LLVM.BuildCall(
                            _builder,
                            structType.StructDecl.InitFunction!.LlvmValue!.Value,
                            arguments,
                            3,
                            "".ToCString()
                        );
                    }

                    ArcRetain(malloc);
                    _current.Block!.ValuesToArcUpdate.Add(malloc);

                    return malloc;
                }

            }

            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(CheckedGroupExpression groupExpression)
        {
            return Next(groupExpression.Expression);
        }

        public LLVMValueRef Visit(CheckedBlockExpression blockExpression)
        {
            var parentStatementValue = _current.Parent!.Statement?.LlvmValue;
            var previousEnvironment = _current.SymbolEnvironment;
            _current.SymbolEnvironment = blockExpression.Environment;
            _current.Block = blockExpression;

            // If the parent is a function declaration
            CheckedFunctionDeclStatement? functionDeclStatement = null;
            if (_current.Parent.Statement is CheckedFunctionDeclStatement decl)
                functionDeclStatement = decl;

            // Functions have their own block starts
            if (functionDeclStatement != null)
            {
                blockExpression.LlvmValue = functionDeclStatement.BlockLlvmValue;
                LLVM.PositionBuilderAtEnd(_builder, functionDeclStatement.BlockLlvmValue!.Value);
            }
            else if (parentStatementValue != null)
            {
                LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                    parentStatementValue!.Value, // Parent function
                    "entry".ToCString()
                );
                blockExpression.LlvmValue = block;
                LLVM.PositionBuilderAtEnd(_builder, block);
            }

            LLVMValueRef? returnValue = null;
            bool manualReturn = false;
            foreach (var (statement, i) in blockExpression.Statements.WithIndex())
            {
                bool isLast = i == blockExpression.Statements.Count - 1;

                if (isLast && blockExpression.ReturnsLastExpression)
                {
                    returnValue = Next(((CheckedExpressionStatement)statement).Expression);
                }
                else
                {
                    Next(statement);
                    if (statement is CheckedReturnStatement)
                    {
                        manualReturn = true;
                        break;
                    }
                }
            }

            // If there is a value to be returned,
            // and the block belongs to a function,
            // and it's returning an object
            if (returnValue != null &&
                functionDeclStatement != null &&
                functionDeclStatement.Body!.DataType is StructType)
            {
                ArcRetain(returnValue!.Value);
            }

            // Arc release if the return isn't done manually,
            // since return statements deal with this themselves.
            if (!manualReturn)
            {
                foreach (var value in blockExpression.ValuesToArcUpdate)
                    ArcRelease(value);
            }

            // If the parent expects the block expression to
            // assign the return value to an alloca (which is outside of the block).
            if (_current.Parent!.BlockReturnValueAlloca != null)
            {
                LLVM.BuildStore(
                    _builder,
                    returnValue!.Value,
                    _current.Parent!.BlockReturnValueAlloca!.Value
                );
            }

            // If it belongs to a function
            // Build the appropriate return statement for the function
            if (functionDeclStatement != null &&
                functionDeclStatement.Body!.DataType?.Type != TypeKeyword.Void)
            {
                if (functionDeclStatement.Body!.ReturnsLastExpression)
                {
                    LLVM.BuildRet(
                        _builder,
                        returnValue!.Value
                    );
                }
            }
            else if (functionDeclStatement != null)
            {
                LLVM.BuildRetVoid(_builder);
            }

            _current.SymbolEnvironment = previousEnvironment;
            _current.Block = null;

            return returnValue ?? null;
        }

        public LLVMValueRef Visit(CheckedVariableExpression variableExpression)
        {
            string identifier = variableExpression.Identifier.Value;
            var variableDecl = variableExpression.VariableDecl!;
            var functionDecl = _current.FunctionDecl!;

            // If it's an object field, get the pointer through the object,
            // If it's a function parameter, get the pointer through the function,
            // otherwise get the variable declaration llvm value.
            LLVMValueRef loadPointer;
            if (variableDecl.VariableType == VariableType.Object) // Object field
            {
                var objectInstance = variableExpression.ObjectInstance == null
                    ? (LLVMValueRef)LLVM.GetParam(functionDecl.LlvmValue!.Value, 0)
                    : Next(variableExpression.ObjectInstance);

                loadPointer = LLVM.BuildStructGEP(
                    _builder,
                    objectInstance,
                    (uint)variableDecl.IndexInObject,
                    "field".ToCString()
                );
            }
            else if (variableDecl.VariableType == VariableType.FunctionParameter) // Function parameter
            {
                // Get the parameter index
                int paramIndex = functionDecl.Parameters.FindIndex(x =>
                    x.Identifier.Value == variableExpression.Identifier.Value
                );

                // The LLVM parameter index will be offset by one if the function belongs to an object.
                uint paramOffset = functionDecl.IsMethod ? 1u : 0u;

                return LLVM.GetParam(
                    functionDecl.LlvmValue!.Value,
                    (uint)paramIndex + paramOffset
                );
            }
            else // Local variable
            {
                loadPointer = variableDecl.LlvmValue!.Value;
            }

            return _current.ShouldBeLoaded
                ? LLVM.BuildLoad(
                    _builder,
                    loadPointer,
                    ("l" + identifier).ToCString()
                )
                : loadPointer;
        }

        public LLVMValueRef Visit(CheckedCallExpression callExpression)
        {
            var functionDecl = callExpression.FunctionSymbol!.Checked!;
            int argumentOffset = callExpression.ObjectInstance == null ? 0 : 1;
            int argumentCount = callExpression.Arguments.Count + argumentOffset;

            fixed (LLVMOpaqueValue** arguments = new LLVMOpaqueValue*[argumentCount])
            {
                if (callExpression.ObjectInstance != null)
                {
                    arguments[0] = Next(callExpression.ObjectInstance);
                }

                // Generate all the arguments
                foreach (var (argument, i) in callExpression.Arguments.WithIndex())
                {
                    arguments[i + argumentOffset] = Next(argument);
                }

                var identifier = callExpression.FunctionSymbol.Checked!.FullName;
                var call = LLVM.BuildCall(
                    _builder,
                    functionDecl.LlvmValue!.Value,
                    arguments,
                    (uint)argumentCount,
                    (functionDecl.ReturnType.Type == TypeKeyword.Void ? "" : identifier).ToCString()
                );

                if (functionDecl.ReturnType is StructType)
                {
                    _current.Block!.ValuesToArcUpdate.Add(call);
                }

                return call;
            }
        }

        public LLVMValueRef Visit(CheckedTypeExpression typeExpression)
        {
            if (typeExpression.DataType is not StructType structType) return null;

            // If a type is being used before the object has been emitted,
            // generate the object first.
            if (structType.StructDecl.LlvmType == null) Next(structType.StructDecl);

            return null;
        }

        public LLVMValueRef Visit(CheckedIfExpression ifExpression)
        {
            LLVMValueRef parent = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));

            // Blocks
            LLVMBasicBlockRef thenBasicBlock = LLVM.AppendBasicBlock(parent, "then".ToCString());
            LLVMBasicBlockRef elseBasicBlock = LLVM.AppendBasicBlock(parent, "else".ToCString());
            LLVMBasicBlockRef mergeBasicBlock = LLVM.AppendBasicBlock(parent, "ifcont".ToCString());

            bool returnsValue = ifExpression.DataType!.Type != TypeKeyword.Void;
            if (returnsValue)
            {
                // Allocate space for the return value,
                // since it is an if *expression*, that can return a value.
                // The block expression will use this alloca and give it a value.
                _current.BlockReturnValueAlloca = LLVM.BuildAlloca(
                    _builder,
                    ifExpression.DataType!.ToLlvmType(_module.Prelude),
                    "retVal".ToCString()
                );
            }

            // Build condition
            var condition = Next(ifExpression.Condition);
            LLVM.BuildCondBr(
                _builder,
                condition,
                thenBasicBlock,
                elseBasicBlock
            );

            // Then branch
            SetCurrentBlock(thenBasicBlock);
            Next(ifExpression.Branch); // Generate branch code
            LLVM.BuildBr(_builder, mergeBasicBlock); // Redirect to merge

            // Else branch
            SetCurrentBlock(elseBasicBlock);
            if (ifExpression.ElseBranch != null) Next(ifExpression.ElseBranch); // Generate branch code if else statement is present
            LLVM.BuildBr(_builder, mergeBasicBlock); // Redirect to merge

            SetCurrentBlock(mergeBasicBlock);

            return returnsValue
                ? LLVM.BuildLoad(
                    _builder,
                    _current.BlockReturnValueAlloca!.Value,
                    "retVal".ToCString()
                )
                : null;
        }

        public LLVMValueRef Visit(CheckedNewExpression newExpression)
        {
            var objectDecl = ((StructType)newExpression.DataType!).StructDecl;
            var type = objectDecl.LlvmType!.Value;
            var malloc = LLVM.BuildMalloc(
                _builder,
                type,
                "new".ToCString()
            );

            if (objectDecl.InitFunction != null)
            {
                // Arguments
                int argumentCount = newExpression.Arguments.Count + 1;
                fixed (LLVMOpaqueValue** arguments = new LLVMOpaqueValue*[argumentCount])
                {
                    // Generate all the arguments
                    foreach (var (argument, i) in newExpression.Arguments.WithIndex())
                    {
                        arguments[i + 1] = Next(argument);
                    }

                    // Call the constructor in this particular object
                    arguments[0] = malloc;

                    LLVM.BuildCall(
                        _builder,
                        objectDecl.InitFunction.LlvmValue!.Value,
                        arguments,
                        (uint)argumentCount,
                        "".ToCString()
                    );
                }
            }

            ArcRetain(malloc);
            _current.Block!.ValuesToArcUpdate.Add(malloc);

            return malloc;
        }

        public LLVMValueRef Visit(CheckedDotExpression dotExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(CheckedKeywordValueExpression keywordValueExpression)
        {
            if (keywordValueExpression.TokenKind == TokenKind.Self)
            {
                var function = _current.FunctionDecl!.LlvmValue!.Value;

                return LLVM.GetParam(function, 0);
            }
            else if (keywordValueExpression.TokenKind == TokenKind.True)
            {
                return LLVM.ConstInt(LLVM.Int1Type(), 1, 0);
            }
            else if (keywordValueExpression.TokenKind == TokenKind.False)
            {
                return LLVM.ConstInt(LLVM.Int1Type(), 0, 0);
            }

            return null!;
        }

        private void ArcRetain(LLVMValueRef objectPointer)
        {
            var obj = _module.Prelude?.Modules["object"].GetClass("object")?.Checked ??
                _module.Root.Modules["object"].GetClass("object")!.Checked;
            var retain = obj!.GetFunction("retain")!.Checked!.LlvmValue;

            fixed (LLVMOpaqueValue** args = new LLVMOpaqueValue*[1])
            {
                args[0] = LLVM.BuildBitCast(
                    _builder,
                    objectPointer,
                    LLVM.PointerType(obj.LlvmType!.Value, 0),
                    "toObj".ToCString()
                );
                LLVM.BuildCall(
                    _builder,
                    retain!.Value,
                    args,
                    1,
                    "".ToCString()
                );
            }
        }

        private void ArcRelease(LLVMValueRef objectPointer)
        {
            var obj = _module.Prelude?.Modules["object"].GetClass("object")?.Checked ??
                _module.Root.Modules["object"].GetClass("object")!.Checked;
            var release = obj!.GetFunction("release")!.Checked!.LlvmValue;

            fixed (LLVMOpaqueValue** args = new LLVMOpaqueValue*[1])
            {
                args[0] = LLVM.BuildBitCast(
                    _builder,
                    objectPointer,
                    LLVM.PointerType(obj.LlvmType!.Value, 0),
                    "toObj".ToCString()
                );
                LLVM.BuildCall(
                    _builder,
                    release!.Value,
                    args,
                    1,
                    "".ToCString()
                );
            }
        }

        private void SetCurrentBlock(LLVMBasicBlockRef basicBlock)
        {
            LLVM.PositionBuilderAtEnd(_builder, basicBlock);
            _current.Block!.LlvmValue = basicBlock;
        }

        private void TemporarilySetCurrentBlock(LLVMBasicBlockRef basicBlock)
        {
            LLVM.PositionBuilderAtEnd(_builder, basicBlock);
        }

        private void DeselectTemporaryBlock()
        {
            LLVM.PositionBuilderAtEnd(_builder, _current.Block!.LlvmValue!.Value);
        }
    }
}