using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public partial class DotExpression : Expression
    {
        public List<Expression> Expressions { get; }

        public DotExpression(List<Expression> expressions)
            : base(expressions.First().Span.Add(expressions.Last().Span))
        {
            Expressions = expressions;
        }
    }
}