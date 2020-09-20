using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.AST
{
    public class NewExpression : IExpression
    {
        public List<Token> ModulePath { get; }

        public List<IExpression> Arguments { get; }

        public NewExpression(List<Token> modulePath, List<IExpression> arguments)
        {
            ModulePath = modulePath;
            Arguments = arguments;
        }

        public T Accept<T>(IExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}