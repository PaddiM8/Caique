using System;
using System.Collections.Generic;
using System.Linq;
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

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            var newObjectInstance = ObjectInstance?.Clone(cloningInfo);
            var parentClass = (newObjectInstance?.DataType as StructType)?.StructDecl;
            var newFunctionDecl = parentClass?.GetFunction(
                FunctionDecl.Identifier.Value,
                FunctionDecl.TypeArguments?.Select(x => x.Clone(cloningInfo)).ToList(),
                false
            );
            return new CheckedCallExpression(
                Arguments.CloneExpressions(cloningInfo),
                newFunctionDecl ?? FunctionDecl,
                DataType.Clone(cloningInfo),
                newObjectInstance
            );
        }
    }
}