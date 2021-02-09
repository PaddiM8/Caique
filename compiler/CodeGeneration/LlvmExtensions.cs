using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public static class LlvmExtensions
    {
        public static unsafe LLVMOpaqueType* ToLlvmType(this IDataType dataType, ModuleEnvironment? prelude)
        {
            var keyword = dataType.Type;

            LLVMOpaqueType* type = keyword switch
            {
                TypeKeyword.i8 => LLVM.Int8Type(),
                TypeKeyword.i32 => LLVM.Int32Type(),
                TypeKeyword.i64 => LLVM.Int64Type(),
                TypeKeyword.f8 => LLVM.FloatType(),
                TypeKeyword.f32 => LLVM.FloatType(),
                TypeKeyword.f64 => LLVM.FloatType(),
                TypeKeyword.Bool => LLVM.Int1Type(),
                TypeKeyword.Void => LLVM.VoidType(),
                TypeKeyword.Identifier => LLVM.PointerType(((StructType)dataType).StructDecl.LlvmType!.Value, 0),
                TypeKeyword.StringConstant => prelude!.Modules["string"].GetClass("String")!.LlvmType!.Value,
                TypeKeyword.Unknown => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };

            return dataType.IsExplicitPointer
                ? LLVM.PointerType(type, 0)
                : type;
        }

        public static unsafe LLVMOpaqueType** ToLlvmTypeArray(this ICollection<IDataType> dataTypes, ModuleEnvironment? prelude)
        {
            var llvmTypes = new LLVMOpaqueType*[dataTypes.Count];
            foreach (var (dataType, i) in dataTypes.WithIndex())
            {
                llvmTypes[i] = dataType.ToLlvmType(prelude);
            }

            fixed (LLVMOpaqueType** llvmTypesPointer = llvmTypes)
            {
                return llvmTypesPointer;
            }
        }

        public static LLVMOpcode ToLlvmOpcode(this TokenKind kind, bool isFloat)
        {
            if (isFloat)
            {
                return kind switch
                {
                    TokenKind.Plus => LLVMOpcode.LLVMFAdd,
                    TokenKind.Minus => LLVMOpcode.LLVMFSub,
                    TokenKind.Star => LLVMOpcode.LLVMFMul,
                    TokenKind.Slash => LLVMOpcode.LLVMFDiv,
                    _ => throw new NotImplementedException()
                };
            }
            else
            {
                return kind switch
                {
                    TokenKind.Plus => LLVMOpcode.LLVMAdd,
                    TokenKind.Minus => LLVMOpcode.LLVMSub,
                    TokenKind.Star => LLVMOpcode.LLVMMul,
                    TokenKind.Slash => LLVMOpcode.LLVMSDiv,
                    _ => throw new NotImplementedException()
                };
            }
        }

        public static LLVMIntPredicate ToLlvmIntPredicate(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.EqualsEquals => LLVMIntPredicate.LLVMIntEQ,
                TokenKind.BangEquals => LLVMIntPredicate.LLVMIntNE,
                TokenKind.ClosedAngleBracket => LLVMIntPredicate.LLVMIntSGT,
                TokenKind.MoreOrEquals => LLVMIntPredicate.LLVMIntSGE,
                TokenKind.OpenAngleBracket => LLVMIntPredicate.LLVMIntSLT,
                TokenKind.LessOrEquals => LLVMIntPredicate.LLVMIntSLE,
                _ => throw new NotImplementedException(),
            };
        }

        public static LLVMRealPredicate ToLlvmRealPredicate(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.EqualsEquals => LLVMRealPredicate.LLVMRealOEQ,
                TokenKind.BangEquals => LLVMRealPredicate.LLVMRealONE,
                TokenKind.ClosedAngleBracket => LLVMRealPredicate.LLVMRealOGT,
                TokenKind.MoreOrEquals => LLVMRealPredicate.LLVMRealOGE,
                TokenKind.OpenAngleBracket => LLVMRealPredicate.LLVMRealOLT,
                TokenKind.LessOrEquals => LLVMRealPredicate.LLVMRealOLE,
                _ => throw new NotImplementedException(),
            };
        }
    }
}