using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public class VariableDeclStatement : Statement
    {
        public Token Identifier { get; }

        public Expression? Value { get; }

        public TypeExpression? SpecifiedType { get; }

        public VariableType VariableType { get; }

        public int IndexInObject { get; }

        public override DataType? DataType
        {
            get => SpecifiedType == null ? _dataType : SpecifiedType.DataType;
            set
            {
                if (SpecifiedType == null) _dataType = value;
                else SpecifiedType.DataType = value;
            }
        }

        private DataType? _dataType;

        public VariableDeclStatement(Token identifier,
                                     TextSpan span,
                                     Expression? value,
                                     TypeExpression? type = null,
                                     VariableType variableType = VariableType.Local,
                                     int indexInObject = 0)
                                     : base(span)
        {
            Identifier = identifier;
            Value = value;
            SpecifiedType = type;
            VariableType = variableType;
            IndexInObject = indexInObject;
        }
    }
}