using System;
using Caique.Parsing;

namespace Caique.AST
{
    public class VariableDeclStatement : IStatement
    {
        public Token Identifier { get; }

        public IExpression Value { get; }

        public TypeExpression? Type { get; }

        public VariableDeclStatement(Token identifier, IExpression value, TypeExpression? type = null)
        {
            Identifier = identifier;
            Value = value;
            Type = type;
        }

        public T Accept<T>(IStatementVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}