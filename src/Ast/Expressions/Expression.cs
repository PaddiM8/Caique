using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class Expression
    {
        public TextSpan Span { get; }

        public DataType? DataType { get; set; }

        public Expression(TextSpan span)
        {
            Span = span;
        }
    }
}