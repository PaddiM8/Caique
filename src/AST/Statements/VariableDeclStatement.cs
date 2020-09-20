using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.AST
{
    public class VariableDeclStatement : IStatement
    {
        public Token Identifier { get; }

        public IExpression? Value { get; }

        public TypeExpression? SpecifiedType { get; }

        public DataType? DataType { get; set; }

        public VariableDeclStatement(Token identifier, IExpression? value, TypeExpression? type = null)
        {
            Identifier = identifier;
            Value = value;
            SpecifiedType = type;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}