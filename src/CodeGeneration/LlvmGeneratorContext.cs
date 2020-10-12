using System;
using System.Collections.Generic;
using Caique.Ast;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public class LlvmGeneratorContext
    {
        public LlvmGeneratorContext? Parent { get; init; }

        public LLVMValueRef? LLVMValue { get; set; }

        private readonly Dictionary<string, LLVMValueRef> _variables =
            new Dictionary<string, LLVMValueRef>();

        public LlvmGeneratorContext CreateChild()
        {
            return new LlvmGeneratorContext
            {
                Parent = this
            };
        }

        public void AddVariable(string identifier, LLVMValueRef value)
        {
            _variables.Add(identifier, value);
        }

        public LLVMValueRef? GetVariable(string identifier)
        {
            if (_variables.TryGetValue(identifier, out LLVMValueRef value))
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