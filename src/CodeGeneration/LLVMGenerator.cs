using System;
using System.Collections.Generic;
using Caique.Ast;
using Caique.Semantics;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public unsafe class LLVMGenerator : IStatementVisitor<object>, IExpressionVisitor<LLVMValueRef>
    {
        private readonly AbstractSyntaxTree _ast;
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;
        private readonly Stack<(LLVMValueRef entry, BlockExpression block)> _valueStack =
            new Stack<(LLVMValueRef, BlockExpression)>();

        public LLVMGenerator(AbstractSyntaxTree ast)
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

            while (_valueStack.Count > 0)
            {
                _valueStack.Peek().block.Accept(this);
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
            throw new NotImplementedException();
        }

        public object Visit(VariableDeclStatement variableDeclStatement)
        {
            throw new NotImplementedException();
        }

        public object Visit(ReturnStatement returnStatement)
        {
            throw new NotImplementedException();
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

            _valueStack.Push((function, functionDeclStatement.Body));

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
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(LiteralExpression literalExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(GroupExpression groupExpression)
        {
            throw new NotImplementedException();
        }

        public LLVMValueRef Visit(BlockExpression blockExpression)
        {
            LLVMBasicBlockRef block = LLVM.AppendBasicBlock(
                _valueStack.Pop().entry,
                "entry".ToCString()
            );
            LLVM.PositionBuilderAtEnd(_builder, block);

            foreach (var statement in blockExpression.Statements)
            {
                statement.Accept(this);
            }

            return null!;
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