using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class FunctionDeclStatement : IStatement
    {
        public Token Identifier { get; }

        public List<Parameter> Parameters { get; }

        public BlockExpression Body { get; }

        public TypeExpression? ReturnType { get; }

        public TextSpan Span { get; }

        public FunctionDeclStatement(Token identifier, List<Parameter> parameters,
                                     BlockExpression body, TypeExpression? returnType,
                                     TextSpan span)
        {
            Identifier = identifier;
            Parameters = parameters;
            Body = body;
            ReturnType = returnType;
            Span = span;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}