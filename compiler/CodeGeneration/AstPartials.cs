using System;
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace Caique.CheckedTree
{
    public partial class CheckedExpression
    {
        public LLVMValueRef? LlvmValue { get; set; }
    }

    public partial class CheckedStatement
    {
        public LLVMValueRef? LlvmValue { get; set; }

        public LLVMTypeRef? LlvmType { get; set; }
    }

    public partial class CheckedFunctionDeclStatement
    {
        public LLVMBasicBlockRef? BlockLlvmValue { get; set; }
    }

    public partial class CheckedBlockExpression
    {
        public new LLVMBasicBlockRef? LlvmValue { get; set; }

        public List<LLVMValueRef> ValuesToRelease { get; set; } = new();
    }
}