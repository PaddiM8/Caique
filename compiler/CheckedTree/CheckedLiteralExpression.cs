using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedLiteralExpression : CheckedExpression
    {
        public Token Value { get; }

        public CheckedLiteralExpression(Token value, IDataType dataType)
            : base(dataType)
        {
            Value = value;
        }

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedLiteralExpression(Value, DataType.Clone(cloningInfo));
        }
    }
}