using System;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    class CheckedUnknownExpression : CheckedExpression
    {
        public CheckedUnknownExpression() : base(new PrimitiveType(TypeKeyword.Unknown))
        {
        }
    }
}