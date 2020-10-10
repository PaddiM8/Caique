using System;
using Caique.Semantics;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public static class LLVMExtensions
    {
        public static unsafe LLVMOpaqueType* ToLLVMType(this DataType dataType)
        {
            var keyword = dataType.Type;

            return keyword switch
            {
                TypeKeyword.i8 => throw new NotImplementedException(),
                TypeKeyword.i32 => LLVM.Int32Type(),
                TypeKeyword.i64 => throw new NotImplementedException(),
                TypeKeyword.f8 => throw new NotImplementedException(),
                TypeKeyword.f32 => throw new NotImplementedException(),
                TypeKeyword.f64 => throw new NotImplementedException(),
                TypeKeyword.NumberLiteral => throw new NotImplementedException(),
                TypeKeyword.Bool => throw new NotImplementedException(),
                TypeKeyword.Void => LLVM.VoidType(),
                TypeKeyword.Identifier => throw new NotImplementedException(),
                TypeKeyword.Unknown => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };
        }
    }
}