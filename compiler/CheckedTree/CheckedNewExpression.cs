using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedNewExpression : CheckedExpression
    {
        public List<CheckedExpression> Arguments { get; }

        public CheckedNewExpression(List<CheckedExpression> arguments,
                                    IDataType dataType) : base(dataType)
        {
            Arguments = arguments;
        }

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedNewExpression(
                Arguments.CloneExpressions(cloningInfo),
                DataType.Clone(cloningInfo)
            );
        }
    }
}