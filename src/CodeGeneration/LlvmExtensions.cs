using System;
using Caique.Semantics;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public static class LlvmExtensions
    {
        public static unsafe LLVMOpaqueType* ToLLVMType(this DataType dataType)
        {
            var keyword = dataType.Type;

            return keyword switch
            {
                TypeKeyword.i8 => LLVM.Int8Type(),
                TypeKeyword.i32 => LLVM.Int32Type(),
                TypeKeyword.i64 => LLVM.Int64Type(),
                TypeKeyword.f8 => LLVM.FloatType(),
                TypeKeyword.f32 => LLVM.FloatType(),
                TypeKeyword.f64 => LLVM.FloatType(),
                TypeKeyword.Bool => LLVM.Int1Type(),
                TypeKeyword.Void => LLVM.VoidType(),
                TypeKeyword.Identifier => throw new NotImplementedException(),
                TypeKeyword.Unknown => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };
        }
    }
}