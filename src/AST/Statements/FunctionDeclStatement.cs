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

        public FunctionDeclStatement(Token identifier, List<Parameter> parameters,
                                     BlockExpression body, TypeExpression? returnType)
        {
            Identifier = identifier;
            Parameters = parameters;
            Body = body;
            ReturnType = returnType;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}