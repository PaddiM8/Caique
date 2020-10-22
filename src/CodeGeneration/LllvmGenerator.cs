﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Caique.Ast;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public unsafe class LllvmGenerator : IAstTraverser<LLVMValueRef, LLVMValueRef>
    {
        private readonly AbstractSyntaxTree _ast;
        private readonly LLVMContextRef _context;
        private readonly LLVMModuleRef _llvmModule;
        private readonly LLVMBuilderRef _builder;
        private LlvmGeneratorContext _current = new LlvmGeneratorContext();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Main();

        public LllvmGenerator(AbstractSyntaxTree ast)
        {
            _ast = ast;
            _context = LLVM.ContextCreate();
            _llvmModule = LLVM.ModuleCreateWithName(
                ast.ModuleEnvironment.Identifier.ToCString()
            );
            _builder = LLVM.CreateBuilderInContext(_context);
        }

        public void Generate()
        {
            var functions = new List<FunctionDeclStatement>();
            foreach (var statement in _ast.Statements)
            {
                Next(statement);

                switch (statement)
                {
                    case ClassDeclStatement classDeclStatement:
                        foreach (var function in classDeclStatement.Body.Environment.Functions)
                        {
                            Next(function);
                            functions.Add(function);
                        }
                        break;
                    case FunctionDeclStatement functionDeclStatement:
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

                Next(functionDeclStatement.Body);
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

            // Everything below is temporary code for testing purpsoes
            LLVM.LinkInMCJIT();

            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            var options = new LLVMMCJITCompilerOptions { NoFramePointerElim = 1 };
            var optionsSize = new UIntPtr(1);
            LLVM.InitializeMCJITCompilerOptions(&options, optionsSize);

            LLVMOpaqueExecutionEngine* engine;
            sbyte* error;
            if (LLVM.CreateMCJITCompilerForModule(&engine, _llvmModule, &options, optionsSize, &error) != 0)
            {
                Console.WriteLine($"Error: {*error}");
            }

            LLVMOpaqueValue* mainFnValue;
            if (LLVM.FindFunction(engine, "main".ToCString(), &mainFnValue) == 0)
            {
                var mainFn = (Main)Marshal.GetDelegateForFunctionPointer(
                    (IntPtr)LLVM.GetPointerToGlobal(engine, mainFnValue),
                    typeof(Main)
                );
                float result = mainFn();

                Console.WriteLine("Result: " + result);
            }
            else
            {
                Console.WriteLine("Couldn't find main function.");
            }

            LLVM.ContextDispose(_context);
        }

        private LLVMValueRef Next(Statement statement)
        {
            _current = _current.CreateChild(statement);
            var value = ((IAstTraverser<LLVMValueRef, LLVMValueRef>)this).Next(statement);
            _current = _current.Parent!;

            return value;
        }

        private LLVMValueRef Next(Expression expression)
        {
            _current = _current.CreateChild(expression);
            var value = ((IAstTraverser<LLVMValueRef, LLVMValueRef>)this).Next(expression);
            _current = _current.Parent!;

            return value;
        }


        public LLVMValueRef Visit(ExpressionStatement expressionStatement)
        {
            Next(expressionStatement.Expression);

            return null!;
        }

        public LLVMValueRef Visit(VariableDeclStatement variableDeclStatement)
        {
            string identifier = variableDeclStatement.Identifier.Value;
            LLVMTypeRef type = variableDeclStatement.DataType!.Value.ToLlvmType();

            // Allocate variable
            LLVMValueRef alloca = LLVM.BuildAlloca(
                _builder,
                type,
                identifier.ToCString()
            );

            variableDeclStatement.LlvmValue = alloca;

            if (variableDeclStatement.Value != null)
            {
                LLVMValueRef initializer = Next(variableDeclStatement.Value);
                LLVM.BuildStore(_builder, initializer, alloca);
            }

            return null!;
        }

        public LLVMValueRef Visit(ReturnStatement returnStatement)
        {
            LLVM.BuildRet(
                _builder,
                Next(returnStatement.Expression)
            );

            return null!;
        }

        public LLVMValueRef Visit(AssignmentStatement assignmentStatement)
        {
            // If it's a normal variable, get the LLVM value of its declaration,
            // otherwise just get its pointer.
            LLVMValueRef assignee = assignmentStatement.Assignee is VariableExpression variableExpression
                ? variableExpression.VariableDecl!.LlvmValue!.Value
                : Next(assignmentStatement.Assignee);
            LLVMValueRef value = Next(assignmentStatement.Value);
            LLVM.BuildStore(_builder, value, assignee);

            return null!;
        }

        public LLVMValueRef Visit(FunctionDeclStatement functionDeclStatement)
        {
            var returnType = functionDeclStatement.ReturnType?.DataType
                ?? new DataType(TypeKeyword.Void);
            string identifier = functionDeclStatement.Identifier.Value;
            var parameterDataTypes = new List<DataType>();

            // If it belongs to an object
            int parameterOffset = 0;
            if (functionDeclStatement.ParentObject != null)
            {
                identifier = identifier + "." + functionDeclStatement
                    .ParentObject.Identifier.Value;
                parameterOffset++;

                // Add the parent object type as the first parameter
                parameterDataTypes.Add(functionDeclStatement.ParentObject.DataType!.Value);
            }

            // Add the parameter types to the parameter list
            parameterDataTypes.AddRange(
                functionDeclStatement.Parameters.Select(x => x.Type.DataType!.Value)
            );

            LLVMTypeRef functionType = LLVM.FunctionType(
                returnType.ToLlvmType(),
                parameterDataTypes.ToLlvmTypeArray(),
                (uint)parameterDataTypes.Count,
                0
            );
            LLVMValueRef function = LLVM.AddFunction(
                _llvmModule,
                identifier.ToCString(),
                functionType
            );

            functionDeclStatement.LlvmValue = function;

            return function;
        }

        public LLVMValueRef Visit(ClassDeclStatement classDeclStatement)
        {
            // Create the struct type
            string identifier = classDeclStatement.Identifier.Value;
            var namedStruct = LLVM.StructCreateNamed(
                _context,
                ("class." + identifier).ToCString()
            );

            // Set the structure field types
            var variableDecls = classDeclStatement.Body.Environment.Variables;
            var fieldTypes = variableDecls
                .Select(x => x!.DataType!.Value)
                .ToList()
                .ToLlvmTypeArray();

            LLVM.StructSetBody(
                namedStruct,
                fieldTypes,
                (uint)variableDecls.Count,
                0
            );

            classDeclStatement.LlvmType = namedStruct;

            // Constructor parameters
            var parameterDataTypes = new List<DataType>
            {
                classDeclStatement.DataType!.Value
            };
            parameterDataTypes.AddRange(
                classDeclStatement.ParameterRefDecls!
                    .Select(x => x.DataType!.Value)
            );

            // Constructor function
            LLVMTypeRef constructorFunctionType = LLVM.FunctionType(
                LLVM.VoidType(),
                parameterDataTypes.ToLlvmTypeArray(),
                (uint)parameterDataTypes.Count,
                0
            );
            LLVMValueRef constructorFunction = LLVM.AddFunction(
                _llvmModule,
                (identifier + "." + identifier).ToCString(),
                constructorFunctionType
            );

            // Constructor content
            LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                constructorFunction,
                "entry".ToCString()
            );
            LLVM.PositionBuilderAtEnd(_builder, block);

            // Class parameters
            var firstParameter = LLVM.GetParam(constructorFunction, 0);
            foreach (var (parameter, i) in classDeclStatement.ParameterRefDecls.WithIndex())
            {
                var parameterValue = LLVM.GetParam(constructorFunction, (uint)(i + 1));
                var objectFieldValue = LLVM.BuildStructGEP(
                    _builder,
                    firstParameter,
                    (uint)parameter.IndexInObject,
                    "structField".ToCString()
                );

                LLVM.BuildStore(
                    _builder,
                    parameterValue,
                    objectFieldValue
                );
            }

            if (classDeclStatement.InitBody != null)
            {
                foreach (var initStatement in classDeclStatement.InitBody.Statements)
                {
                    Next(initStatement);
                }
            }

            LLVM.BuildRetVoid(_builder);

            classDeclStatement.InitLlvmValue = constructorFunction;

            return null!;
        }

        public LLVMValueRef Visit(UseStatement useStatement)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(UnaryExpression unaryExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(BinaryExpression binaryExpression)
        {
            bool isFloat = binaryExpression.DataType!.Value.IsFloat();
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

        public LLVMValueRef Visit(LiteralExpression literalExpression)
        {
            var dataType = literalExpression.DataType!.Value;
            if (dataType.IsNumber())
            {
                string tokenValue = literalExpression.Value.Value;

                // Float
                if (dataType.IsFloat())
                {
                    return LLVM.ConstReal(
                        LLVM.FloatType(),
                        double.Parse(tokenValue)
                    );
                }
                else // Int
                {
                    var value = ulong.Parse(tokenValue);
                    return LLVM.ConstInt(
                        LLVM.Int32Type(),
                        value,
                        value < 0 ? 0 : 1
                    );
                }
            }

            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(GroupExpression groupExpression)
        {
            return Next(groupExpression.Expression);
        }

        public LLVMValueRef Visit(BlockExpression blockExpression)
        {
            var parentStatementValue = _current.Parent!.Statement!.LlvmValue;

            if (parentStatementValue != null)
            {
                LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                    parentStatementValue!.Value,
                    "entry".ToCString()
                );
                LLVM.PositionBuilderAtEnd(_builder, block);
            }

            LLVMValueRef? returnValue = null;
            foreach (var (statement, i) in blockExpression.Statements.WithIndex())
            {
                bool isLast = i == blockExpression.Statements.Count - 1;

                if (isLast &&
                    statement is ExpressionStatement expressionStatement &&
                    !expressionStatement.TrailingSemicolon)
                {
                    returnValue = Next(expressionStatement.Expression);
                }
                else
                {
                    Next(statement);
                }
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
            if (_current.Parent.Statement is FunctionDeclStatement functionDeclStatement &&
                functionDeclStatement.Body.DataType?.Type != TypeKeyword.Void)
            {
                LLVM.BuildRet(
                    _builder,
                    returnValue!.Value
                );
            }
            else
            {
                LLVM.BuildRetVoid(_builder);
            }

            return returnValue ?? null;
        }

        public LLVMValueRef Visit(VariableExpression variableExpression)
        {
            string identifier = variableExpression.Identifier.Value;
            return LLVM.BuildLoad(
                _builder,
                variableExpression.VariableDecl!.LlvmValue!.Value,
                ("l" + identifier).ToCString()
            );
        }

        public LLVMValueRef Visit(CallExpression callExpression)
        {
            var identifier = callExpression.ModulePath[^1].Value;
            var functionDecl = callExpression.FunctionDecl!;

            // If the function belongs to an object, reserve the first spot for the object
            int argumentOffset = functionDecl.ParentObject == null ? 0 : 1;
            int argumentCount = callExpression.Arguments.Count + argumentOffset;
            fixed (LLVMOpaqueValue** arguments = new LLVMOpaqueValue*[argumentCount])
            {
                // Generate all the arguments
                foreach (var (argument, i) in callExpression.Arguments.WithIndex())
                {
                    arguments[i + argumentOffset] = Next(argument);
                }

                // If the function belongs to an object, set the first argument to the object,
                // which should be set already in the context.
                if (functionDecl.ParentObject != null)
                {
                    arguments[0] = _current.DotExpressionObject!.Value;
                }

                return LLVM.BuildCall(
                    _builder,
                    functionDecl.LlvmValue!.Value,
                    arguments,
                    (uint)argumentCount,
                    identifier.ToCString()
                );
            }
        }

        public LLVMValueRef Visit(TypeExpression typeExpression)
        {
            throw new InvalidOperationException();
        }

        public LLVMValueRef Visit(IfExpression ifExpression)
        {
            LLVMValueRef parent = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));

            // Blocks
            LLVMBasicBlockRef thenBB = LLVM.AppendBasicBlock(parent, "then".ToCString());
            LLVMBasicBlockRef elseBB = LLVM.AppendBasicBlock(parent, "else".ToCString());
            LLVMBasicBlockRef mergeBB = LLVM.AppendBasicBlock(parent, "ifcont".ToCString());

            bool returnsValue = ifExpression.DataType!.Value.Type != TypeKeyword.Void;
            if (returnsValue)
            {
                // Allocate space for the return value,
                // since it is an if *expression*, that can return a value.
                // The block expression will use this alloca and give it a value.
                _current.BlockReturnValueAlloca = LLVM.BuildAlloca(
                    _builder,
                    ifExpression.DataType!.Value.ToLlvmType(),
                    "retVal".ToCString()
                );
            }

            // Build condition
            LLVM.BuildCondBr(_builder, Next(ifExpression.Condition), thenBB, elseBB);

            // Then branch
            LLVM.PositionBuilderAtEnd(_builder, thenBB); // Position builder at block
            Next(ifExpression.Branch); // Generate branch code
            LLVM.BuildBr(_builder, mergeBB); // Redirect to merge

            // Else branch
            LLVM.PositionBuilderAtEnd(_builder, elseBB); // Position builder at block
            if (ifExpression.ElseBranch != null) Next(ifExpression.ElseBranch); // Generate branch code if else statement is present
            LLVM.BuildBr(_builder, mergeBB); // Redirect to merge

            LLVM.PositionBuilderAtEnd(_builder, mergeBB);

            return returnsValue
                ? LLVM.BuildLoad(
                    _builder,
                    _current.BlockReturnValueAlloca!.Value,
                    "retVal".ToCString()
                )
                : null;
        }

        public LLVMValueRef Visit(NewExpression newExpression)
        {
            var objectDecl = newExpression.DataType!.Value.ObjectDecl!;
            var type = objectDecl.LlvmType;
            var malloc = LLVM.BuildMalloc(
                _builder,
                type!.Value,
                "new".ToCString()
            );

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
                    objectDecl.InitLlvmValue!.Value,
                    arguments,
                    (uint)argumentCount,
                    "".ToCString()
                );
            }

            return malloc;
        }

        public LLVMValueRef Visit(DotExpression dotExpression)
        {
            if (dotExpression.Right is CallExpression _)
            {
                _current.DotExpressionObject = Next(dotExpression.Left);
                var value = Next(dotExpression.Right);
                _current.DotExpressionObject = null;

                return value;
            }
            else if (dotExpression.Right is VariableExpression variableExpression)
            {
                var left = (VariableExpression)dotExpression.Left;
                var variableDecl = left.VariableDecl!;

                // Get the pointer of the field in the struct
                var elementPointer = LLVM.BuildStructGEP(
                    _builder,
                    Next(dotExpression.Left),
                    (uint)variableDecl.IndexInObject,
                    variableExpression.Identifier.Value.ToCString()
                );

                // If it's for an assignment statement, it shouldn't be loaded,
                // since it's just going to be assigned to.
                if (_current.Parent?.Statement is AssignmentStatement)
                {
                    return elementPointer;
                }
                else
                {
                    return LLVM.BuildLoad(
                        _builder,
                        elementPointer,
                        "load".ToCString()
                    );
                }
            }

            throw new InvalidOperationException();
        }
    }
}