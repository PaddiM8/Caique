using System;
using LLVMSharp.Interop;

namespace Caique.Ast
{
    public partial class Statement
    {
        public LLVMValueRef? LlvmValue { get; set; }

        public LLVMTypeRef? LlvmType { get; set; }
    }

    public partial class ClassDeclStatement
    {
        public LLVMValueRef? InitLlvmValue { get; set; }
    }
}