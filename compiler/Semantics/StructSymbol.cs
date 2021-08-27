using System;
using Caique.Ast;
using Caique.CheckedTree;

namespace Caique.Semantics
{
    public class StructSymbol
    {
        public ClassDeclStatement Syntax { get; }

        public CheckedClassDeclStatement? Checked { get; set; }

        public StructSymbol(ClassDeclStatement syntax)
        {
            Syntax = syntax;
        }
    }
}