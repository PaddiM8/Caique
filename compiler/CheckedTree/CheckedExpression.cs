using System;
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
    }
}