using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public partial class Statement
    {
        public TextSpan Span { get; }

        public Statement(TextSpan span)
        {
            Span = span;
        }
    }
}