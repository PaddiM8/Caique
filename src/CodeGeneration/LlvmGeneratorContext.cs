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

        public LLVMValueRef? FunctionCallParentObject { get; set; }

        private readonly Dictionary<string, LLVMValueRef> _variables =
            new Dictionary<string, LLVMValueRef>();

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
                FunctionCallParentObject = expression is CallExpression ? FunctionCallParentObject : null
            };
        }

        public void AddVariable(string identifier, LLVMValueRef value)
        {
            _variables.Add(identifier, value);
        }

        public LLVMValueRef? GetVariable(string identifier)
        {
            if (Expression is BlockExpression &&
                _variables.TryGetValue(identifier, out LLVMValueRef value))
            {
                return value;
            }
            else
            {
                if (Parent == null) return null;

                return Parent.GetVariable(identifier);
            }
        }
    }
}