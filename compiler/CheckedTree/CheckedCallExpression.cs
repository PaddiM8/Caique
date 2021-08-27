using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedCallExpression : CheckedExpression
    {
        public List<CheckedExpression> Arguments { get; }

        public FunctionSymbol FunctionSymbol { get; }

        public CheckedExpression? ObjectInstance { get; }

        public CheckedCallExpression(List<CheckedExpression> arguments,
                                     FunctionSymbol functionSymbol,
                                     IDataType dataType,
                                     CheckedExpression? objectInstance = null) : base(dataType)
        {
            Arguments = arguments;
            FunctionSymbol = functionSymbol;
            ObjectInstance = objectInstance;
        }
    }
}