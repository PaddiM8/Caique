using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedVariableExpression : CheckedExpression
    {
        public Token Identifier { get; }

        public CheckedVariableDeclStatement VariableDecl { get; }

        public CheckedExpression? ObjectInstance { get; }

        public CheckedVariableExpression(Token identifier,
                                         CheckedVariableDeclStatement variableDecl,
                                         IDataType dataType,
                                         CheckedExpression? objectInstance = null)
            : base(dataType)
        {
            Identifier = identifier;
            VariableDecl = variableDecl;
            ObjectInstance = objectInstance;
        }
    }
}