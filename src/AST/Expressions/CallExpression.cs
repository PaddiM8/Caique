using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class CallExpression : IExpression
    {
        public Token Identifier { get; }

        public List<IExpression> Arguments { get; }

        public CallExpression(Token identifier, List<IExpression> arguments)
        {
            Identifier = identifier;
            Arguments = arguments;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}