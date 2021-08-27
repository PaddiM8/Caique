using System;
using Caique.Ast;
using Caique.CheckedTree;

namespace Caique.Semantics
{
    public class FunctionSymbol
    {
        public FunctionDeclStatement Syntax { get; }

        public CheckedFunctionDeclStatement? Checked { get; set; }

        public FunctionSymbol(FunctionDeclStatement syntax)
        {
            Syntax = syntax;
        }
    }
}