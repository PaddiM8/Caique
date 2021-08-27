using System;
using Caique.Ast;
using Caique.CheckedTree;

namespace Caique.Semantics
{
    public class VariableSymbol
    {
        public VariableDeclStatement Syntax { get; }

        public CheckedVariableDeclStatement? Checked { get; set; }

        public SymbolEnvironment Environment { get; }

        public VariableSymbol(VariableDeclStatement syntax, SymbolEnvironment environment)
        {
            Syntax = syntax;
            Environment = environment;
        }
    }
}