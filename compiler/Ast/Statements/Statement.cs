using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public partial class Statement
    {
        public TextSpan Span { get; }

        public virtual IDataType? DataType { get; set; }

        public Statement(TextSpan span)
        {
            Span = span;
        }
    }
}