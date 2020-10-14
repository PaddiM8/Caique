using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Caique.Ast;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public unsafe class LllvmGenerator : IAstTraverser<object, LLVMValueRef>
    {
        private readonly AbstractSyntaxTree _ast;
        private readonly LLVMModuleRef _llvmModule;
        private readonly LLVMBuilderRef _builder;
        private LlvmGeneratorContext _current = new LlvmGeneratorContext();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate float Main();

        public LllvmGenerator(AbstractSyntaxTree ast)
        {
            _ast = ast;
            _llvmModule = LLVM.ModuleCreateWithName(
                ast.ModuleEnvironment.Identifier.ToCString()
            );
            _builder = LLVM.CreateBuilder();
        }

        public void Generate()
        {
            foreach (var statement in _ast.Statements)
            {
                Next(statement);
            }

            foreach (var statement in _ast.Statements)
            {
                if (statement is FunctionDeclStatement functionDeclStatement)
                {
                    _current = new LlvmGeneratorContext
                    {
                        Parent = null,
                        Statement = statement
                    };

                    var bodyValue = Next(functionDeclStatement.Body);

                }
            }

            sbyte* moduleError;
            _ = LLVM.VerifyModule(
                _llvmModule,
                LLVMVerifierFailureAction.LLVMPrintMessageAction,
                &moduleError
            );
            if (moduleError != null) Console.WriteLine(*moduleError);

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

            LLVM.DumpModule(_llvmModule);
        }

        private void Next(Statement statement)
        {
            _current = _current.CreateChild(statement);
            ((IAstTraverser<object, LLVMValueRef>)this).Next(statement);
            _current = _current.Parent!;
        }

        private LLVMValueRef Next(Expression expression)
        {
            _current = _current.CreateChild(expression);
            var value = ((IAstTraverser<object, LLVMValueRef>)this).Next(expression);
            _current = _current.Parent!;

            return value;
        }


        public object Visit(ExpressionStatement expressionStatement)
        {
            Next(expressionStatement.Expression);

            return null!;
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            string identifier = variableDeclStatement.Identifier.Value;

            // Allocate variable
            LLVMValueRef alloca = LLVM.BuildAlloca(
                _builder,
                variableDeclStatement.DataType!.Value.ToLLVMType(),
                identifier.ToCString()
            );

            _current.Parent!.AddVariable(identifier, alloca);

            if (variableDeclStatement.Value != null)
            {
                LLVMValueRef initializer = Next(variableDeclStatement.Value);
                LLVM.BuildStore(_builder, initializer, alloca);
            }

            return null!;
        }

        public object Visit(ReturnStatement returnStatement)
        {
            LLVM.BuildRet(
                _builder,
                Next(returnStatement.Expression)
            );

            return null!;
        }

        public object Visit(AssignmentStatement assignmentStatement)
        {
            LLVMValueRef value = Next(assignmentStatement.Value);
            var leftIdentifier = assignmentStatement.Variable.Identifier.Value;
            var variableRef = _current.GetVariable(leftIdentifier)!.Value;
            LLVM.BuildStore(_builder, value, variableRef);

            return null!;
        }

        public object Visit(FunctionDeclStatement functionDeclStatement)
        {
            var returnType = functionDeclStatement.ReturnType?.DataType
                ?? new DataType(TypeKeyword.Void);

            LLVMTypeRef functionType = LLVM.FunctionType(
                returnType.ToLLVMType(),
                null,
                0,
                0
            );

            LLVMValueRef function = LLVM.AddFunction(
                _llvmModule,
                functionDeclStatement.Identifier.Value.ToCString(),
                functionType
            );

            functionDeclStatement.LlvmValue = function;

            return null!;
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            throw new NotImplementedException();
        }

        public object Visit(UseStatement useStatement)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(UnaryExpression unaryExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(BinaryExpression binaryExpression)
        {
            LLVMOpcode opcode;
            if (binaryExpression.DataType!.Value.IsInt())
            {
                opcode = binaryExpression.Operator.Kind switch
                {
                    TokenKind.Plus => LLVMOpcode.LLVMAdd,
                    TokenKind.Minus => LLVMOpcode.LLVMSub,
                    TokenKind.Star => LLVMOpcode.LLVMMul,
                    TokenKind.Slash => LLVMOpcode.LLVMSDiv,
                    _ => throw new NotImplementedException()
                };
            }
            else if (binaryExpression.DataType!.Value.IsFloat())
            {
                opcode = binaryExpression.Operator.Kind switch
                {
                    TokenKind.Plus => LLVMOpcode.LLVMFAdd,
                    TokenKind.Minus => LLVMOpcode.LLVMFSub,
                    TokenKind.Star => LLVMOpcode.LLVMFMul,
                    TokenKind.Slash => LLVMOpcode.LLVMFDiv,
                    _ => throw new NotImplementedException()
                };
            }
            else
            {
                throw new NotImplementedException();
            }

            return LLVM.BuildBinOp(
                _builder,
                opcode,
                Next(binaryExpression.Left),
                Next(binaryExpression.Right),
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
            _current.IsBlock = true;

            LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                _current.Parent!.Statement!.LlvmValue!.Value,
                "entry".ToCString()
            );
            LLVM.PositionBuilderAtEnd(_builder, block);

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

            // If it belongs to a function
            if (_current.Parent.Statement is FunctionDeclStatement functionDeclStatement)
            {
                // Build the appropriate return statement for the function
                if (functionDeclStatement.Body.DataType?.Type == TypeKeyword.Void)
                {
                    LLVM.BuildRetVoid(_builder);
                }
                else
                {
                    LLVM.BuildRet(
                        _builder,
                        returnValue
                    );
                }
            }

            return returnValue;
        }

        public LLVMValueRef Visit(VariableExpression variableExpression)
        {
            string identifier = variableExpression.Identifier.Value;
            var value = _current.GetVariable(identifier)!.Value;
            value = LLVM.BuildLoad(
                _builder,
                value,
                ("l" + identifier).ToCString()
            );

            return value;
        }

        public LLVMValueRef Visit(CallExpression callExpression)
        {
            var identifier = callExpression.ModulePath[^1].Value;
            return LLVM.BuildCall(
                _builder,
                callExpression.FunctionDecl!.LlvmValue!.Value,
                null,
                0,
                identifier.ToCString()
            );
        }

        public LLVMValueRef Visit(TypeExpression typeExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(IfExpression ifExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(NewExpression newExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(DotExpression dotExpression)
        {
            throw new NotImplementedException();
        }
    }
}