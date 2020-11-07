using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class NewExpression : Expression
    {
        public TypeExpression Type { get; }

        public List<Expression> Arguments { get; }

        public NewExpression(TypeExpression type, List<Expression> arguments,
                             TextSpan span) : base(span)
        {
            Type = type;
            Arguments = arguments;
        }
    }
}