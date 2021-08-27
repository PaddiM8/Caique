using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedTypeExpression : CheckedExpression
    {
        public CheckedTypeExpression(IDataType dataType)
            : base(dataType)
        {
        }
    }
}