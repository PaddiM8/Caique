using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedCallExpression : CheckedExpression
    {
        public List<CheckedExpression> Arguments { get; }

        public CheckedFunctionDeclStatement FunctionDecl { get; }

        public CheckedExpression? ObjectInstance { get; }

        public CheckedCallExpression(List<CheckedExpression> arguments,
                                     CheckedFunctionDeclStatement functionDecl,
                                     IDataType dataType,
                                     CheckedExpression? objectInstance = null) : base(dataType)
        {
            Arguments = arguments;
            FunctionDecl = functionDecl;
            ObjectInstance = objectInstance;
        }
    }
}