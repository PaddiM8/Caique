using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedParameter
    {
        public Token Identifier { get; }

        public IDataType? DataType { get; set; }

        public CheckedParameter(Token identifier, IDataType? dataType)
        {
            Identifier = identifier;
            DataType = dataType;
        }
    }
}