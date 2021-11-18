using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public partial class CheckedExpression
    {
        public IDataType DataType { get; }

        public CheckedExpression(IDataType dataType)
        {
            DataType = dataType;
        }

        public virtual CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            throw new NotImplementedException();
        }
    }
}