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

        public bool IsBlock { get; set; }

        private readonly Dictionary<string, LLVMValueRef> _variables =
            new Dictionary<string, LLVMValueRef>();

        public LlvmGeneratorContext CreateChild(Statement statement)
        {
            return new LlvmGeneratorContext
            {
                Parent = this,
                Statement = statement
            };
        }

        public LlvmGeneratorContext CreateChild(Expression expression)
        {
            return new LlvmGeneratorContext
            {
                Parent = this,
                Expression = expression
            };
        }

        public void AddVariable(string identifier, LLVMValueRef value)
        {
            _variables.Add(identifier, value);
        }

        public LLVMValueRef? GetVariable(string identifier)
        {
            if (IsBlock && _variables.TryGetValue(identifier, out LLVMValueRef value))
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