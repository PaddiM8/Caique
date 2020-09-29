using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class ClassDeclStatement : IStatement
    {
        public Token Identifier { get; }

        public List<Token> ParameterRefs { get; }

        public BlockExpression Body { get; }

        public TypeExpression? Ancestor { get; }

        public TextSpan Span { get; }

        public ClassDeclStatement(Token identifier,
                                  List<Token> parameterRefs,
                                  BlockExpression body,
                                  TextSpan span,
                                  TypeExpression? ancestor = null)
        {
            Identifier = identifier;
            ParameterRefs = parameterRefs;
            Body = body;
            Ancestor = ancestor;
            Span = span;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}