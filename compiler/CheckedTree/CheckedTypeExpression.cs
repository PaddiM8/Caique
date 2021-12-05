using System;
using System.Collections.Generic;
using System.Linq;
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

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedTypeExpression(DataType.Clone(cloningInfo));
        }
    }
}