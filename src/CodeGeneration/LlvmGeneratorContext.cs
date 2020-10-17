using System;
using System.Collections.Generic;
using Caique.Ast;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public class LlvmGeneratorContext
    {
        public LlvmGeneratorContext? Parent { get; init; }

        public Statement? Statement { get; init; }

        public Expression? Expression { get; init; }

        public LLVMValueRef? BlockReturnValueAlloca { get; set; }

        public LLVMValueRef? DotExpressionObject { get; set; }

        public LlvmGeneratorContext CreateChild(Statement statement)
        {
            return new LlvmGeneratorContext
            {
                Parent = this,
                Statement = statement,
                BlockReturnValueAlloca = BlockReturnValueAlloca,
            };
        }

        public LlvmGeneratorContext CreateChild(Expression expression)
        {
            return new LlvmGeneratorContext
            {
                Parent = this,
                Expression = expression,
                BlockReturnValueAlloca = BlockReturnValueAlloca,
                DotExpressionObject = DotExpressionObject,
            };
        }
    }
}