using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class TypeExpression : Expression
    {
        public List<Token> ModulePath { get; }

        public ModuleEnvironment? ImportedModule { get; set; }

        public TypeExpression(List<Token> modulePath)
            : base(modulePath[0].Span.Add(modulePath[^1].Span))
        {
            ModulePath = modulePath;
        }
    }
}