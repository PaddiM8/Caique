using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    class CheckedUnknownExpression : CheckedExpression
    {
        public CheckedUnknownExpression() : base(new PrimitiveType(TypeKeyword.Unknown))
        {
        }

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedUnknownExpression();
        }
    }
}