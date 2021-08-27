using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedDotExpression : CheckedExpression
    {
        public List<CheckedExpression> Expressions { get; }

        public CheckedDotExpression(List<CheckedExpression> expressions, IDataType dataType)
            : base(dataType)
        {
            Expressions = expressions;
        }
    }
}