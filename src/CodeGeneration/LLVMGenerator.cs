using System;
using Caique.Ast;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public unsafe class LLVMGenerator : IStatementVisitor<object>, IExpressionVisitor<LLVMValueRef>
    {
        private readonly AbstractSyntaxTree _ast;
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;

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

            sbyte* moduleError;
            LLVM.VerifyModule(
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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