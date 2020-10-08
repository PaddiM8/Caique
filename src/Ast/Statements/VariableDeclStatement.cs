using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class VariableDeclStatement : IStatement
    {
        public Token Identifier { get; }

        public IExpression? Value { get; }

        public TypeExpression? SpecifiedType { get; }

        public DataType? DataType { get; set; }

        public TextSpan Span { get; }

        public VariableDeclStatement(Token identifier, TextSpan span,
                                     IExpression? value, TypeExpression? type = null)
        {
            Identifier = identifier;
            Value = value;
            SpecifiedType = type;
            Span = span;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}