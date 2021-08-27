using System;
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
    }
}