using System;
using System.Collections.Generic;
using Caique.Ast;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public unsafe class LllvmGenerator : IStatementVisitor<object>, IExpressionVisitor<LLVMValueRef>
    {
        private readonly AbstractSyntaxTree _ast;
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;
        private LllvmGeneratorEnvironment _environment = new LllvmGeneratorEnvironment();

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
                statement.Accept(this);
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

        public object Visit(ExpressionStatement expressionStatement)
        {
            expressionStatement.Expression.Accept(this);

            ClearEnvironment();
            return null!;
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public object Visit(ReturnStatement returnStatement)
        {
            LLVM.BuildRet(
                _builder,
                returnStatement.Expression.Accept(this)
            );

            ClearEnvironment();
            return null!;
        }

        public object Visit(AssignmentStatement assignmentStatement)
        {
            ClearEnvironment();
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

            _environment.ParentValue = function;
            var bodyValue = functionDeclStatement.Body.Accept(this);

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

            ClearEnvironment();
            return null!;
        }

        public object Visit(ClassDeclStatement classDeclStatement)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public object Visit(UseStatement useStatement)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(UnaryExpression unaryExpression)
        {
            ClearEnvironment();
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

            ClearEnvironment();
            return LLVM.BuildBinOp(
                _builder,
                opcode,
                binaryExpression.Left.Accept(this),
                binaryExpression.Right.Accept(this),
                "binOp".ToCString()
            );
        }

        public LLVMValueRef Visit(LiteralExpression literalExpression)
        {
            var dataType = literalExpression.DataType!.Value;
            if (dataType.Type == TypeKeyword.NumberLiteral)
            {
                string tokenValue = literalExpression.Value.Value;

                // Float
                if (tokenValue.Contains("."))
                {
                    ClearEnvironment();
                    return LLVM.ConstReal(
                        LLVM.FloatType(),
                        double.Parse(tokenValue)
                    );
                }
                else // Int
                {
                    var value = ulong.Parse(tokenValue);
                    ClearEnvironment();
                    return LLVM.ConstInt(
                        LLVM.Int32Type(),
                        value,
                        value < 0 ? 0 : 1
                    );
                }
            }

            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(GroupExpression groupExpression)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(BlockExpression blockExpression)
        {
            LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                _environment.ParentValue!.Value,
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
                    ClearEnvironment();
                    return expressionStatement.Expression.Accept(this);
                }
                else
                {
                    statement.Accept(this);
                }
            }

            ClearEnvironment();
            return null;
        }

        public LLVMValueRef Visit(VariableExpression variableExpression)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(CallExpression callExpression)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(TypeExpression typeExpression)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(IfExpression ifExpression)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(NewExpression newExpression)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(DotExpression dotExpression)
        {
            ClearEnvironment();
            throw new NotImplementedException();
        }

        private void ClearEnvironment()
        {
            _environment = new LllvmGeneratorEnvironment();
        }
    }
}