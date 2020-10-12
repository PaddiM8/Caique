using System;
using System.Collections.Generic;
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
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;
        private LlvmGeneratorContext _current = new LlvmGeneratorContext();

        public LllvmGenerator(AbstractSyntaxTree ast)
        {
            _ast = ast;
            _module = LLVM.ModuleCreateWithName(
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

            sbyte* moduleError;
            _ = LLVM.VerifyModule(
                _module,
                LLVMVerifierFailureAction.LLVMPrintMessageAction,
                &moduleError
            );
            if (moduleError != null) Console.WriteLine(*moduleError);
            LLVM.DumpModule(_module);
        }

        private void Next(Statement statement)
        {
            _current = _current.CreateChild();
            ((IAstTraverser<object, LLVMValueRef>)this).Next(statement);
            _current = _current.Parent!;
        }

        private LLVMValueRef Next(Expression expression)
        {
            _current = _current.CreateChild();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
                _module,
                functionDeclStatement.Identifier.Value.ToCString(),
                functionType
            );

            _current.LLVMValue = function;
            var bodyValue = Next(functionDeclStatement.Body);

            // If void function
            if (functionDeclStatement.Body.DataType?.Type == TypeKeyword.Void)
            {
                LLVM.BuildRetVoid(_builder);
            }
            else
            {
                LLVM.BuildRet(
                    _builder,
                    bodyValue
                );
            }

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
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(BlockExpression blockExpression)
        {
            LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                _current.Parent!.LLVMValue!.Value,
                "entry".ToCString()
            );
            LLVM.PositionBuilderAtEnd(_builder, block);

            foreach (var (statement, i) in blockExpression.Statements.WithIndex())
            {
                bool isLast = i == blockExpression.Statements.Count - 1;

                if (isLast &&
                    statement is ExpressionStatement expressionStatement &&
                    !expressionStatement.TrailingSemicolon)
                {
                    return Next(expressionStatement.Expression);
                }
                else
                {
                    Next(statement);
                }
            }

            return null;
        }

        public LLVMValueRef Visit(VariableExpression variableExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(CallExpression callExpression)
        {
            throw new NotImplementedException();
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