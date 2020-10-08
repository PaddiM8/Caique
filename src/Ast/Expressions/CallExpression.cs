using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class CallExpression : IExpression
    {
        public List<Token> ModulePath { get; }

        public List<IExpression> Arguments { get; }

        public TextSpan Span { get; }

        public CallExpression(List<Token> modulePath, List<IExpression> arguments, TextSpan span)
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