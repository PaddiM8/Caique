using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public interface IExpression
    {
        TextSpan Span { get; }

        T Accept<T>(IExpressionVisitor<T> visitor);
    }
}