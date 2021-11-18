using System;
using System.Collections.Generic;
using Caique.Ast;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedVariableDeclStatement : CheckedStatement
    {
        public Token Identifier { get; }

        public CheckedExpression? Value { get; }

        public IDataType DataType { get; set; }

        public VariableType VariableType { get; }

        public int IndexInObject { get; set; }

        public int? IndexInObjectAfterInheritance { get; set; }

        public CheckedVariableDeclStatement(Token identifier,
                                            CheckedExpression? value,
                                            IDataType dataType,
                                            VariableType variableType = VariableType.Local,
                                            int indexInObject = 0)
        {
            Identifier = identifier;
            Value = value;
            DataType = dataType;
            VariableType = variableType;
            IndexInObject = indexInObject;
        }

        public override CheckedStatement Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedVariableDeclStatement(
                Identifier,
                Value?.Clone(cloningInfo),
                DataType.Clone(cloningInfo),
                VariableType,
                IndexInObject
            );
        }
    }
}