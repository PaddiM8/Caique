using System;
using System.Collections.Generic;
using LLVMSharp.Interop;

namespace Caique.Ast
{
    public partial class Statement
    {
        public LLVMValueRef? LlvmValue { get; set; }

        public LLVMTypeRef? LlvmType { get; set; }
    }

    public partial class FunctionDeclStatement
    {
        public LLVMBasicBlockRef? BlockLlvmValue { get; set; }
    }

    public partial class BlockExpression
    {
        public LLVMBasicBlockRef? LlvmValue { get; set; }

        public List<LLVMValueRef> ValuesToArcUpdate { get; set; } = new();
    }

    public partial class DotExpression
    {
        public LLVMValueRef? LlvmValue { get; set; }
    }
}