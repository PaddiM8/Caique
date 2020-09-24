using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class ClassDeclStatement : IStatement
    {
        public Token Identifier { get; }

        public BlockExpression Body { get; }

        public TypeExpression? Ancestor { get; }

        public TextSpan Span { get; }

        public ClassDeclStatement(Token identifier, BlockExpression body,
                                  TextSpan span, TypeExpression? ancestor = null)
        {
            Identifier = identifier;
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