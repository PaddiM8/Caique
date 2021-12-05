using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public partial class FunctionDeclStatement : Statement, IGenericTypeOrigin
    {
        public Token Identifier { get; }

        public string FullName => IsExtensionFunction
            ? ExtensionOf!.FullName + "." + Identifier.Value
            : Identifier.Value;

        public List<Token>? TypeParameters { get; }

        public List<VariableDeclStatement> Parameters { get; }

        public BlockExpression? Body { get; }

        public TypeExpression? ReturnType { get; }

        public bool IsInitFunction { get; set; }

        public bool IsVirtual { get; set; }
        public bool IsOverride { get; set; }

        public bool IsMethod { get; }

        public TypeExpression? ExtensionOf { get; set; }

        public bool IsExtensionFunction => ExtensionOf != null;

        public FunctionDeclStatement(Token identifier,
                                     List<Token>? typeParameters,
                                     List<VariableDeclStatement> parameters,
                                     BlockExpression? body,
                                     TypeExpression? returnType,
                                     bool isMethod,
                                     bool isInitFunction,
                                     bool isVirtual,
                                     bool isOverride,
                                     TextSpan span) : base(span)
        {
            Identifier = identifier;
            TypeParameters = typeParameters;
            Parameters = parameters;
            Body = body;
            IsMethod = isMethod;
            IsInitFunction = isInitFunction;
            IsVirtual = isVirtual;
            IsOverride = isOverride;
            ReturnType = returnType;
        }
    }
}