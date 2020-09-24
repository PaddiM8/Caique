using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class NewExpression : IExpression
    {
        public List<Token> ModulePath { get; }

        public List<IExpression> Arguments { get; }

        public TextSpan Span { get; }

        public NewExpression(List<Token> modulePath, List<IExpression> arguments,
                             TextSpan span)
        {
            ModulePath = modulePath;
            Arguments = arguments;
            Span = span;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}