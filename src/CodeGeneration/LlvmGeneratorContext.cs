using System;
using Caique.Ast;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public class LlvmGeneratorContext
    {
        public LlvmGeneratorContext? Parent { get; init; }

        public LLVMValueRef? LLVMValue { get; set; }

        public LlvmGeneratorContext CreateChild()
        {
            return new LlvmGeneratorContext
            {
                Parent = this
            };
        }
    }
}