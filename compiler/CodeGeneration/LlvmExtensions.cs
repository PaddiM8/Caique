using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public static class LlvmExtensions
    {
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