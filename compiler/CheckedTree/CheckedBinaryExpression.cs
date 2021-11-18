using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedBinaryExpression : CheckedExpression
    {
        public CheckedExpression Left { get; }

        public Token Operator { get; }

        public CheckedExpression Right { get; }

        public CheckedBinaryExpression(CheckedExpression left, Token op, CheckedExpression right, IDataType dataType)
            : base(dataType)
        {
            Left = left;
            Operator = op;
            Right = right;
        }

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedBinaryExpression(
                Left.Clone(cloningInfo),
                Operator,
                Right.Clone(cloningInfo),
                DataType.Clone(cloningInfo)
            );
        }
    }
}