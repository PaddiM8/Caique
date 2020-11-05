using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public record Parameter
    {
        public Token Identifier { get; }

        public TypeExpression? Type { get; set; }

        public bool IsReference { get => Type == null; }

        public DataType? DataType
        {
            get => Type == null ? _dataType : Type.DataType;
            set
            {
                if (Type == null) _dataType = value;
                else Type.DataType = value;
            }
        }

        private DataType? _dataType;

        public Parameter(Token identifier, TypeExpression? type)
        {
            Identifier = identifier;
            Type = type;
        }
    }
}