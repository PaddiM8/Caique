using System;
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
                                            IDataType type,
                                            VariableType variableType = VariableType.Local,
                                            int indexInObject = 0)
        {
            Identifier = identifier;
            Value = value;
            DataType = type;
            VariableType = variableType;
            IndexInObject = indexInObject;
        }
    }
}