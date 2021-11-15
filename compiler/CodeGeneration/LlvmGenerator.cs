using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LLVMSharp.Interop;
using Caique.Ast;
using Caique.CheckedTree;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;

namespace Caique.CodeGeneration
{
    public unsafe class LlvmGenerator : ICheckedTreeTraverser<LLVMValueRef, LLVMValueRef>
    {
        private readonly ModuleEnvironment _module;
        private readonly LLVMContextRef _context;
        private readonly LLVMModuleRef _llvmModule;
        private readonly LLVMBuilderRef _builder;
        private LlvmGeneratorContext _current = new();
        private LLVMValueRef? _mallocValue = null;
        private LLVMTypeRef? _mallocType = null;
        private readonly List<CheckedFunctionDeclStatement> _functions = new();
        private readonly List<CheckedClassDeclStatement> _classes = new();

        private readonly HashSet<string> _declaredExternalSymbols = new();

        public LlvmGenerator(ModuleEnvironment module)
        {
            _module = module;
            _context = LLVM.ContextCreate();
            _llvmModule = LLVM.ModuleCreateWithName(
                _module.Identifier.ToCString()
            );
            _builder = LLVM.CreateBuilderInContext(_context);
        }

        ~LlvmGenerator()
        {
            LLVM.ContextDispose(_context);
        }

        public void GenerateSymbols()
        {
            foreach (var classSymbol in _module.SymbolEnvironment.Classes)
                GenerateClassSymbol(classSymbol);

            foreach (var functionSymbol in _module.SymbolEnvironment.Functions)
                GenerateFunctionSymbol(functionSymbol);
        }

        public void GenerateContent()
        {
            foreach (var classSymbol in _module.SymbolEnvironment.Classes)
            {
                foreach (var checkedClass in classSymbol.AllChecked)
                    GenerateClassContent(checkedClass);
            }

            foreach (var checkedFunction in _functions)
                GenerateFunctionContent(checkedFunction);
        }

        public void GenerateClassSymbol(StructSymbol symbol)
        {
            foreach (var checkedClass in symbol.AllChecked)
            {
                if (symbol.Syntax.TypeParameters != null && checkedClass.TypeArguments == null)
                    continue;

                _current.ClassDecl = checkedClass;

                // May already have been done by TypeExpression.
                if (checkedClass.LlvmType == null)
                    Next(checkedClass);

                if (checkedClass.InitFunction != null)
                {
                    if (checkedClass.InitFunction.LlvmValue == null)
                        Next(checkedClass.InitFunction);

                    _functions.Add(checkedClass.InitFunction);
                }

                foreach (var function in checkedClass.Body!.Environment.Functions)
                {
                    var checkedFunction = checkedClass.GetFunction(function.Syntax.FullName);
                    Next(checkedFunction!);
                    if (checkedFunction!.Body != null) _functions.Add(checkedFunction);
                }
            }

            _current.ClassDecl = null;
        }

        public void GenerateClassContent(CheckedClassDeclStatement checkedClass)
        {
            // Set the struct field types
            var fieldTypeMap = new Dictionary<int, LLVMTypeRef>();

            CheckedClassDeclStatement? ancestor = checkedClass.Inherited;
            while (ancestor != null)
            {
                foreach (var variable in ancestor.Body!.Environment.Variables)
                {
                    fieldTypeMap.Add(
                        variable!.Checked!.IndexInObjectAfterInheritance!.Value,
                        ToLlvmType(variable.Checked.DataType!)
                    );
                }

                ancestor = ancestor.Inherited;
            }

            var variableDecls = checkedClass.Body!.Environment.Variables;
            int inheritedFieldCount = fieldTypeMap.Count;
            foreach (var variableSymbol in variableDecls)
            {
                var variableDecl = variableSymbol!.Checked!;

                // The first items in the struct will be the inherited ones,
                // so the IndexInObject value needs to be offset.
                if (variableDecl.IndexInObjectAfterInheritance == null)
                {
                    variableDecl.IndexInObjectAfterInheritance = variableDecl.IndexInObject + inheritedFieldCount;
                }

                fieldTypeMap.Add(
                    variableDecl.IndexInObjectAfterInheritance!.Value,
                    ToLlvmType(variableDecl!.DataType)
                );
            }
            
            var fieldTypes = new LLVMTypeRef[fieldTypeMap.Count];
            foreach (var (i, dataType) in fieldTypeMap)
            {
                fieldTypes[i] = dataType;
            }

            // Class id
            //fieldTypes.Add(LLVM.Int32Type());

            checkedClass.InternalFieldCount = fieldTypeMap.Count;

            LLVM.StructSetBody(
                checkedClass.LlvmType!.Value,
                ToCArray(fieldTypes),
                (uint)fieldTypeMap.Count,
                0
            );

            /*if (checkedClass.InitFunction != null &&
                _current.Parent?.Expression is not CheckedTypeExpression)
            {
                Next(checkedClass.InitFunction);
            }*/
        }

        public void GenerateFunctionSymbol(FunctionSymbol symbol)
        {
            foreach (var checkedFunction in symbol.AllChecked)
            {
                _current.ClassDecl = checkedFunction.ParentObject;
                Next(checkedFunction);

                if (checkedFunction.Body != null)
                    _functions.Add(checkedFunction);
                
                _current.ClassDecl = null;
            }
        }

        public void GenerateFunctionContent(CheckedFunctionDeclStatement checkedFunction)
        {
            _current = new LlvmGeneratorContext
            {
                Parent = null,
                Statement = checkedFunction
            };

            LLVM.PositionBuilderAtEnd(_builder, checkedFunction.BlockLlvmValue!.Value);

            _current.ClassDecl = checkedFunction.ParentObject;
            _current.FunctionDecl = checkedFunction;
            Next(checkedFunction.Body!);
            _current.ClassDecl = null;
        }

        public void GenerateLlvmFile(string targetDirectory)
        {
            sbyte* error;
            int _2 = LLVM.PrintModuleToFile(
                _llvmModule,
                Path.Join(targetDirectory, _module.Identifier + ".ll").ToCString(),
                &error
            );
        }

        public void GenerateObjectFile(string targetDirectory)
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

            _ = LLVM.TargetMachineEmitToFile(
                targetMachine,
                _llvmModule,
                (targetDirectory + "/" + _module.Identifier + ".o").ToCString(),
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
            LLVMTypeRef type = ToLlvmType(variableDeclStatement.DataType!);

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
            foreach (var valueToRelease in _current.Block!.ValuesToRelease)
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
            if (assignmentStatement.Assignee.DataType.IsAllocated)
                ArcRelease(LLVM.BuildLoad(_builder, assignmentStatement.Assignee.LlvmValue!.Value, "".ToCString()));

            _current.ShouldBeLoaded = false;
            LLVMValueRef assignee = Next(assignmentStatement.Assignee);
            _current.ShouldBeLoaded = true;
            LLVMValueRef value = Next(assignmentStatement.Value);
            LLVM.BuildStore(_builder, value, assignee);

            // ARC increment the new reference
            //if (assignmentStatement.Assignee.DataType.IsAllocated)
            //    ArcRetain(LLVM.BuildLoad(_builder, assignee, "".ToCString()));

            return null!;
        }

        public LLVMValueRef Visit(CheckedFunctionDeclStatement functionDeclStatement)
        {
            string identifier = functionDeclStatement.FullName;
            var parameterDataTypes = new List<IDataType>();

            // If it belongs to an object
            if (functionDeclStatement.ParentObject != null)
            {
                // Add the parent object type as the first parameter
                parameterDataTypes.Add(functionDeclStatement.ParentObject.DataType!);
            }

            if (functionDeclStatement.IsExtensionFunction)
            {
                identifier = identifier + "." + functionDeclStatement.ExtensionOf;

                // Add the extended type as the first parameter
                parameterDataTypes.Add(functionDeclStatement.ExtensionOf!);
            }

            // Add the parameter types to the parameter list
            foreach (var parameter in functionDeclStatement.Parameters)
            {
                parameterDataTypes.Add(parameter.DataType);
            }

            LLVMTypeRef functionType = LLVM.FunctionType(
                ToLlvmType(functionDeclStatement.ReturnType),
                ToLlvmTypeArray(parameterDataTypes),
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

            if (functionDeclStatement.FullName == "malloc" && functionDeclStatement.Body == null)
            {
                _mallocValue = function;
                _mallocType = functionType;
            }

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
            classDeclStatement.LlvmType = LLVM.StructCreateNamed(
                _context,
                ("class." + identifier).ToCString()
            );

            return null;
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
                        ToLlvmType(dataType),
                        (ulong)tokenValue[0],
                        1
                    );
                }

                // Float
                if (dataType.IsFloat)
                {
                    return LLVM.ConstReal(
                        ToLlvmType(dataType),
                        double.Parse(tokenValue)
                    );
                }
                else // Int
                {
                    ulong value = ulong.Parse(tokenValue);
                    return LLVM.ConstInt(
                        ToLlvmType(dataType),
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
                    var malloc = BuildMalloc(
                        structType.StructDecl.LlvmType!.Value,
                        structType.StructDecl
                    );

                    var length = LLVM.ConstInt(
                        ToLlvmType(new PrimitiveType(TypeKeyword.isize)),
                        (ulong)literalExpression.Value.Value.Length,
                        1
                    );
                    var shouldFree = LLVM.ConstInt(LLVM.Int1Type(), 0, 0);

                    BuildCall(
                        structType.StructDecl.InitFunction!.LlvmValue!.Value,
                        structType.StructDecl.InitFunction!.LlvmType!.Value,
                        new List<LLVMValueRef>() { malloc, stringPtr, length, shouldFree },
                        structType.StructDecl.Module != _module
                    );

                    _current.Block!.ValuesToRelease.Add(malloc);

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
            if (_current.Parent!.Statement is CheckedFunctionDeclStatement decl)
                functionDeclStatement = decl;

            // Functions have their own block starts
            if (functionDeclStatement != null)
            {
                LLVM.PositionBuilderAtEnd(_builder, functionDeclStatement.BlockLlvmValue!.Value);
                blockExpression.LlvmValue = functionDeclStatement.BlockLlvmValue;
                _current.BlockLlvmValue = blockExpression.LlvmValue;

                foreach (var (parameter, i) in functionDeclStatement.Parameters.WithIndex())
                {
                    if (parameter.DataType.IsAllocated)
                    {
                        var param = LLVM.GetParam(functionDeclStatement.LlvmValue!.Value, (uint)i);
                        ArcRetain(param);
                        _current.Block.ValuesToRelease.Add(param);
                    }
                }
            }
            else if (parentStatementValue != null)
            {
                LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                    parentStatementValue!.Value, // Parent function
                    "entry".ToCString()
                );
                blockExpression.LlvmValue = block;
                _current.BlockLlvmValue = blockExpression.LlvmValue;
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
                foreach (var value in blockExpression.ValuesToRelease)
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
                bool prevShouldBeLoaded = _current.ShouldBeLoaded;
                _current.ShouldBeLoaded = true;
                var objectInstance = variableExpression.ObjectInstance == null
                    ? GetMethodObjectInstance(functionDecl.LlvmValue!.Value)
                    : Next(variableExpression.ObjectInstance);
                _current.ShouldBeLoaded = prevShouldBeLoaded;

                loadPointer = LLVM.BuildStructGEP(
                    _builder,
                    objectInstance,
                    (uint)variableDecl.IndexInObjectAfterInheritance!,
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
            int argumentOffset = callExpression.ObjectInstance == null ? 0 : 1;
            int argumentCount = callExpression.Arguments.Count + argumentOffset;

            var arguments = callExpression.Arguments.Select(x => Next(x)).ToList();
            if (callExpression.ObjectInstance != null)
            {
                var objectPtr = Next(callExpression.ObjectInstance);
                var expectedInstanceDecl = callExpression.FunctionDecl.ParentObject!;
                if (callExpression.ObjectInstance.DataType is StructType gotObjectInstanceType &&
                    expectedInstanceDecl != gotObjectInstanceType.StructDecl)
                {
                    objectPtr = LLVM.BuildBitCast(
                        _builder,
                        objectPtr,
                        LLVM.PointerType(expectedInstanceDecl.LlvmType!.Value, 0),
                        "cast".ToCString()
                    );
                }

                arguments.Insert(0, objectPtr);
            }

            var functionDecl = callExpression.FunctionDecl;
            bool isExternal = functionDecl.Module != _module;
            LLVMValueRef call;
            if (functionDecl.IsVirtual)
            {
                call = BuildCallToVirtual(functionDecl, arguments);
            }
            else
            {
                string name = functionDecl.ReturnType.Type == TypeKeyword.Void
                    ? ""
                    : functionDecl.FullName;
                call = BuildCall(
                    functionDecl.LlvmValue!.Value,
                    functionDecl.LlvmType!.Value,
                    arguments,
                    isExternal,
                    name
                );
            }

            if (functionDecl.ReturnType is StructType)
            {
                _current.Block!.ValuesToRelease.Add(call);
            }

            return call;
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
                    ToLlvmType(ifExpression.DataType!),
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
            var malloc = BuildMalloc(type, objectDecl);

            if (objectDecl.InitFunction != null)
            {
                BuildCall(
                    objectDecl.InitFunction.LlvmValue!.Value,
                    objectDecl.InitFunction.LlvmType!.Value,
                    newExpression.Arguments
                        .Select(x => Next(x))
                        .Prepend(malloc)
                        .ToList(),
                    objectDecl.InitFunction.Module != _module
                );
            }

            _current.Block!.ValuesToRelease.Add(malloc);

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

                return GetMethodObjectInstance(function);
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

        private LLVMValueRef BuildCall(LLVMValueRef functionValue,
                                    LLVMTypeRef functionType,
                                    ICollection<LLVMValueRef> args,
                                    bool isExternal,
                                    string name = "")
        {
            fixed (LLVMOpaqueValue** argumentsPtr = new LLVMOpaqueValue*[args.Count])
            {
                foreach (var (argument, i) in args.WithIndex())
                {
                    argumentsPtr[i] = argument;
                }

                if (isExternal && !_declaredExternalSymbols.Contains(functionValue.Name))
                {
                    LLVM.AddFunction(_llvmModule, functionValue.Name.ToCString(), functionType);
                    _declaredExternalSymbols.Add(functionValue.Name);
                }

                return LLVM.BuildCall(
                    _builder,
                    functionValue,
                    argumentsPtr,
                    (uint)args.Count,
                    name.ToCString()
                );
            }
        }

        private void ArcRetain(LLVMValueRef objectPointer)
        {
            var obj = _module.Prelude?.Modules["object"].GetClass("object")?.AllChecked.First() ??
                _module.Root.Modules["object"].GetClass("object")!.AllChecked.First();
            var retain = obj!.GetFunction("retain")!;
            var arg = LLVM.BuildBitCast(
                _builder,
                objectPointer,
                LLVM.PointerType(obj.LlvmType!.Value, 0),
                "toObj".ToCString()
            );

            BuildCall(
                retain.LlvmValue!.Value,
                retain.LlvmType!.Value,
                new List<LLVMValueRef>() { arg },
                true
            );
        }

        private void ArcRelease(LLVMValueRef objectPointer)
        {
            var obj = _module.Prelude?.Modules["object"].GetClass("object")?.AllChecked.First() ??
                _module.Root.Modules["object"].GetClass("object")!.AllChecked.First();
            var release = obj!.GetFunction("release")!;
            var arg = LLVM.BuildBitCast(
                _builder,
                objectPointer,
                LLVM.PointerType(obj.LlvmType!.Value, 0),
                "toObj".ToCString()
            );

            BuildCall(
                release.LlvmValue!.Value,
                release.LlvmType!.Value,
                new List<LLVMValueRef>() { arg },
                true
            );
        }

        /*private void AddVirtualMethodTableForClass(CheckedClassDeclStatement checkedClass)
        {
            if (checkedClass.VirtualMethods == null) return;

            var elementTypes = new LLVMTypeRef[checkedClass.VirtualMethods.Count];
            foreach (var virtualMethod in checkedClass.VirtualMethods)
            {
                int index = virtualMethod.IndexInVirtualMethodTable!.Value;
                var returnType = virtualMethod.ReturnType.IsAllocated
                    ? (LLVMTypeRef)LLVM.PointerType(LLVM.VoidType(), 0)
                    : ToLlvmType(virtualMethod.ReturnType!);
                elementTypes[index] = LLVM.PointerType(
                    LLVM.FunctionType(returnType, null, 0, 1),
                    0
                );
            }

            var vtableType = LLVM.StructType(
                ToCArray(elementTypes),
                (uint)checkedClass.VirtualMethods.Count,
                0
            );
            var vtableGlobal = LLVM.AddGlobal(_llvmModule, vtableType, "vtable".ToCString());
            checkedClass.VirtualMethodTableLlvmType = LLVM.PointerType(vtableType, 0);
            checkedClass.VirtualMethodTableLlvmValue = vtableGlobal;
        }

        private void FillVirtualMethodTableForClass(CheckedClassDeclStatement checkedClass)
        {
            if (checkedClass.VirtualMethods == null) return;

            var vtableType = checkedClass.VirtualMethodTableLlvmType;
            var vtableSize = checkedClass.VirtualMethods.Count;

            fixed (LLVMOpaqueValue** vtableValues = new LLVMOpaqueValue*[vtableSize])
            {
                foreach (var (virtualMethod, i) in checkedClass.VirtualMethods.WithIndex())
                {
                    var returnType = virtualMethod.ReturnType.IsAllocated
                        ? (LLVMTypeRef)LLVM.PointerType(LLVM.VoidType(), 0)
                        : ToLlvmType(virtualMethod.ReturnType!);
                    vtableValues[i]  = LLVM.ConstPointerCast(
                        virtualMethod.LlvmValue!.Value,
                        LLVM.PointerType(LLVM.FunctionType(returnType, null, 0, 1), 0)
                    );
                }


                LLVM.SetInitializer(
                    checkedClass.VirtualMethodTableLlvmValue!.Value,
                    LLVM.ConstStruct(vtableValues, (uint)vtableSize, 0)
                );
            }
        }*/

        private LLVMValueRef BuildMalloc(LLVMTypeRef type, CheckedClassDeclStatement? checkedClass = null)
        {
            if (_mallocValue == null)
            {
                _mallocType = LLVM.FunctionType(
                    LLVM.PointerType(LLVM.Int8Type(), 0),
                    ToLlvmTypeArray(new List<IDataType> { new PrimitiveType(TypeKeyword.isize) }),
                    1,
                    0
                );
                _mallocValue = LLVM.AddFunction(
                    _llvmModule,
                    "malloc".ToCString(),
                    _mallocType!.Value
                );
            }

            var call = BuildCall(
                _mallocValue!.Value,
                _mallocType!.Value,
                new List<LLVMValueRef> { LLVM.SizeOf(type) },
                true
            );

            var malloc = LLVM.BuildBitCast(
                _builder,
                call,
                LLVM.PointerType(type, 0),
                "cast".ToCString()
            );

            // After instantiating an object, set it's id.
            // This can't be done in the constructor, since
            // inherited classes constructors are being called as well,
            // which set the id too.
            if (checkedClass != null)
            {
                var id = LLVM.BuildStructGEP(
                    _builder,
                    malloc,
                    0,
                    "idField".ToCString()
                );
                LLVM.BuildStore(
                    _builder,
                    LLVM.ConstInt(LLVM.Int32Type(), (ulong)checkedClass.Id!, 0),
                    id
                );
            }

            return malloc;
        }

        public static LLVMValueRef GetMethodObjectInstance(LLVMValueRef method)
        {
            return LLVM.GetParam(method, 0);
        }

        public LLVMValueRef BuildCallToVirtual(CheckedFunctionDeclStatement checkedFunction,
                                               List<LLVMValueRef> arguments)
        {
            var objectIdPtr = LLVM.BuildStructGEP(
                _builder,
                GetMethodObjectInstance(_current.FunctionDecl!.LlvmValue!.Value),
                0,
                "".ToCString()
            );
            var objectId = LLVM.BuildLoad(
                _builder,
                objectIdPtr,
                "objectId".ToCString()
            );

            var mergeBlock = LLVM.AppendBasicBlock(
                LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder)),
                "switchcont".ToCString()
            );
            var switchValue = LLVM.BuildSwitch(
                _builder,
                objectId,
                mergeBlock,
                (uint)checkedFunction.Overrides!.Count
            );

            foreach (var (id, overrideFunction) in checkedFunction.Overrides)
            {
                var callBlock = LLVM.AppendBasicBlock(
                    _current.FunctionDecl!.LlvmValue!.Value,
                    $"id{id}".ToCString()
                );
                LLVM.AddCase(
                    switchValue,
                    LLVM.ConstInt(LLVM.Int32Type(), (ulong)id, 0),
                    callBlock
                );
                LLVM.PositionBuilderAtEnd(_builder, callBlock);

                if (overrideFunction != checkedFunction)
                {
                    arguments[0] = LLVM.BuildBitCast(
                        _builder,
                        GetMethodObjectInstance(_current.FunctionDecl!.LlvmValue!.Value),
                        LLVM.PointerType(overrideFunction.ParentObject!.LlvmType!.Value, 0),
                        "cast".ToCString()
                    );
                }

                BuildCall(
                    overrideFunction.LlvmValue!.Value,
                    overrideFunction.LlvmType!.Value,
                    arguments,
                    overrideFunction.Module != _module
                );
                LLVM.BuildBr(_builder, mergeBlock);
            }

            SetCurrentBlock(mergeBlock);

            return switchValue;
            /*var vtablePtr = LLVM.BuildStructGEP(
                _builder,
                GetMethodObjectInstance(checkedFunction.LlvmValue!.Value),
                (uint)checkedFunction.ParentObject!.InternalFieldCount! - 1,
                "".ToCString()
            );
            var vtable = LLVM.BuildLoad(
                _builder,
                vtablePtr,
                "vtable".ToCString()
            );

            var functionPtrPtr = LLVM.BuildStructGEP(
                _builder,
                vtable,
                (uint)checkedFunction.IndexInVirtualMethodTable!,
                "".ToCString()
            );

            return LLVM.BuildLoad(
                _builder,
                functionPtrPtr,
                "functionPtr".ToCString()
            );*/
        }

        private void SetCurrentBlock(LLVMBasicBlockRef basicBlock)
        {
            LLVM.PositionBuilderAtEnd(_builder, basicBlock);
            _current.Block!.LlvmValue = basicBlock;
            _current.BlockLlvmValue = basicBlock;
        }

        private void TemporarilySetCurrentBlock(LLVMBasicBlockRef basicBlock)
        {
            LLVM.PositionBuilderAtEnd(_builder, basicBlock);
        }

        private void DeselectTemporaryBlock()
        {
            LLVM.PositionBuilderAtEnd(_builder, _current.BlockLlvmValue!.Value);
        }

        private unsafe LLVMTypeRef ToLlvmType(IDataType dataType)
        {
            var keyword = dataType.Type;

            LLVMOpaqueType* type = keyword switch
            {
                TypeKeyword.i8 => LLVM.Int8Type(),
                TypeKeyword.i32 => LLVM.Int32Type(),
                TypeKeyword.i64 => LLVM.Int64Type(),
                TypeKeyword.isize => LLVM.Int64Type(), // TODO: Should depend on target platform
                TypeKeyword.f8 => LLVM.FloatType(),
                TypeKeyword.f32 => LLVM.FloatType(),
                TypeKeyword.f64 => LLVM.FloatType(),
                TypeKeyword.Bool => LLVM.Int1Type(),
                TypeKeyword.Generic => ToLlvmType(_current.ClassDecl!.TypeArguments![((GenericType)dataType).ParameterIndex]),
                TypeKeyword.Void => LLVM.VoidType(),
                TypeKeyword.Identifier => LLVM.PointerType(((StructType)dataType).StructDecl.LlvmType!.Value, 0),
                TypeKeyword.StringConstant => _module.Prelude!.Modules["string"].GetClass("String")!.AllChecked.First().LlvmType!.Value,
                TypeKeyword.Unknown => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };

            return dataType.IsExplicitPointer
                ? LLVM.PointerType(type, 0)
                : type;
        }

        private unsafe LLVMOpaqueType** ToLlvmTypeArray(ICollection<IDataType> dataTypes)
        {
            var llvmTypes = new LLVMOpaqueType*[dataTypes.Count];
            foreach (var (dataType, i) in dataTypes.WithIndex())
            {
                llvmTypes[i] = ToLlvmType(dataType);
            }

            fixed (LLVMOpaqueType** llvmTypesPointer = llvmTypes)
            {
                return llvmTypesPointer;
            }
        }

        private static unsafe LLVMOpaqueType** ToCArray(ICollection<LLVMTypeRef> collection)
        {
            fixed(LLVMOpaqueType** values = new LLVMOpaqueType*[collection.Count])
            {
                foreach (var (value, i) in collection.WithIndex())
                    values[i] = value;

                return values;
            }
        }
    }
}