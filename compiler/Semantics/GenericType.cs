using System;
using Caique.Ast;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique type.
    /// </summary>
    public class GenericType : IDataType
    {
        public TypeKeyword Type => TypeKeyword.Generic;

        public Token Identifier { get; set; }

        public int ParameterIndex { get; set; }

        public bool IsExplicitPointer { get; set; }

        public bool IsAllocated => false;

        public bool IsNumber => false;

        public bool IsString => false;

        public bool IsFloat => false;

        public bool IsInt => false;

        public GenericType(Token identifier, int parameterIndex, bool isExplicitPointer = false)
        {
            Identifier = identifier;
            ParameterIndex = parameterIndex;
            IsExplicitPointer = isExplicitPointer;
        }

        public bool IsCompatible(IDataType expected)
        {
            return expected is GenericType expectedGenericType &&
                ParameterIndex == expectedGenericType.ParameterIndex;
        }

        public override string ToString()
        {
            return Identifier.Value;
        }
    }
}