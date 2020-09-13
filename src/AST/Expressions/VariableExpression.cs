using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class VariableExpression : IExpression
    {
        public List<Token> Identifiers { get; }

        public VariableExpression(List<Token> identifiers)
        {
            Identifiers = identifiers;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}