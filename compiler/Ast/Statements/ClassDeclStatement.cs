using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public partial class ClassDeclStatement : Statement
    {
        public Token Identifier { get; }

        public List<Token>? TypeParameters { get; }

        public BlockExpression Body { get; }

        public FunctionDeclStatement? InitFunction { get; set; }

        public SymbolEnvironment SymbolEnvironment { get; }

        public ModuleEnvironment Module { get; }

        public TypeExpression? InheritedType { get; set; }

        public ClassDeclStatement(Token identifier,
                                  List<Token>? typeParameters,
                                  BlockExpression body,
                                  TextSpan span,
                                  ModuleEnvironment moduleEnvironment,
                                  SymbolEnvironment symbolEnvironment,
                                  TypeExpression? ancestor = null,
                                  FunctionDeclStatement? initFunction = null) : base(span)
        {
            Identifier = identifier;
            TypeParameters = typeParameters;
            Body = body;
            InheritedType = ancestor;
            Module = moduleEnvironment;
            SymbolEnvironment = symbolEnvironment;
            InitFunction = initFunction;
        }
    }
}