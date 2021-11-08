using System;
using System.Collections.Generic;
using Caique.CheckedTree;
using Caique.Semantics;
using LLVMSharp.Interop;

namespace Caique.CodeGeneration
{
    public class LlvmGeneratorContext
    {
        public LlvmGeneratorContext? Parent { get; init; }

        public CheckedStatement? Statement { get; init; }

        public CheckedExpression? Expression { get; init; }

        public LLVMValueRef? BlockReturnValueAlloca { get; set; }

        public LLVMValueRef? DotExpressionObject { get; set; }

        public CheckedClassDeclStatement? ClassDecl { get; set; }

        public CheckedFunctionDeclStatement? FunctionDecl { get; set; }

        public SymbolEnvironment? SymbolEnvironment { get; set; }

        public bool ShouldBeLoaded { get; set; } = true;

        public CheckedBlockExpression? Block { get; set; }

        public LlvmGeneratorContext CreateChild(CheckedStatement statement)
        {
            return new LlvmGeneratorContext
            {
                Parent = this,
                Statement = statement,
                BlockReturnValueAlloca = BlockReturnValueAlloca,
                ClassDecl = ClassDecl,
                FunctionDecl = FunctionDecl,
                SymbolEnvironment = SymbolEnvironment,
                Block = Block,
            };
        }

        public LlvmGeneratorContext CreateChild(CheckedExpression expression)
        {
            return new LlvmGeneratorContext
            {
                Parent = this,
                Expression = expression,
                BlockReturnValueAlloca = BlockReturnValueAlloca,
                DotExpressionObject = DotExpressionObject,
                ClassDecl = ClassDecl,
                FunctionDecl = FunctionDecl,
                SymbolEnvironment = SymbolEnvironment,
                ShouldBeLoaded = ShouldBeLoaded,
                Block = Block,
            };
        }
    }
}