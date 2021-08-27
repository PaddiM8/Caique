using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public partial class ClassDeclStatement : Statement
    {
        public Token Identifier { get; }

        public BlockExpression Body { get; }

        public FunctionDeclStatement? InitFunction { get; set; }

        public ModuleEnvironment Module { get; }

        public TypeExpression? InheritedType { get; set; }

        public ClassDeclStatement(Token identifier,
                                  BlockExpression body,
                                  TextSpan span,
                                  ModuleEnvironment module,
                                  TypeExpression? ancestor = null,
                                  FunctionDeclStatement? initFunction = null) : base(span)
        {
            Identifier = identifier;
            Body = body;
            InheritedType = ancestor;
            Module = module;
            InitFunction = initFunction;
        }
    }
}