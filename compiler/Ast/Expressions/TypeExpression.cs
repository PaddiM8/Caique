using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class TypeExpression : Expression
    {
        public List<Token> ModulePath { get; }

        public List<TypeExpression>? TypeArguments { get; }

        public bool IsExplicitPointer { get; set; }

        public string FullName =>
            string.Join("->", ModulePath.Select(x => x.Value)) + (IsExplicitPointer ? "*" : "");

        public TypeExpression(List<Token> modulePath,
                              List<TypeExpression>? typeArguments,
                              bool isExplicitPointer = false)
            : base(modulePath[0].Span.Add(modulePath[^1].Span))
        {
            ModulePath = modulePath;
            TypeArguments = typeArguments;
            IsExplicitPointer = isExplicitPointer;
        }
    }
}