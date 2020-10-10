using System;
using Caique.Ast;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public class LllvmGeneratorEnvironment
    {
        public LLVMValueRef? ParentValue { get; set; }
    }
}