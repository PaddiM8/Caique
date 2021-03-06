﻿using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public partial class FunctionDeclStatement : Statement
    {
        public Token Identifier { get; }

        public List<Parameter> Parameters { get; }

        public BlockExpression? Body { get; }

        public TypeExpression? ReturnType { get; }

        public ClassDeclStatement? ParentObject { get; set; }

        public bool IsInitFunction { get; set; }

        public bool IsMethod { get => ParentObject != null; }

        public TypeExpression? ExtensionOf { get; set; }

        public bool IsExtensionFunction => ExtensionOf != null;

        public FunctionDeclStatement(Token identifier,
                                     List<Parameter> parameters,
                                     BlockExpression? body,
                                     TypeExpression? returnType,
                                     bool isInitFunction,
                                     TextSpan span) : base(span)
        {
            Identifier = identifier;
            Parameters = parameters;
            Body = body;
            IsInitFunction = isInitFunction;
            ReturnType = returnType;
        }
    }
}