using System;
using Caique.Ast;
using Caique.CheckedTree;
using Caique.Parsing;

namespace Caique.Semantics
{
    public enum GenericTypeOrigin
    {
        Class,
        Function,
    }

    /// <summary>
    /// Represents a Caique type.
    /// </summary>
    public class GenericType : IDataType
    {
        public TypeKeyword Type => TypeKeyword.Generic;

        public Token Identifier { get; set; }

        public int ParameterIndex { get; set; }

        public GenericTypeOrigin Origin { get; }

        public bool IsExplicitPointer { get; set; }

        public bool IsAllocated => false;

        public bool IsNumber => false;

        public bool IsString => false;

        public bool IsFloat => false;

        public bool IsInt => false;

        public GenericType(Token identifier,
                           int parameterIndex,
                           GenericTypeOrigin origin,
                           bool isExplicitPointer = false)
        {
            Identifier = identifier;
            ParameterIndex = parameterIndex;
            Origin = origin;
            IsExplicitPointer = isExplicitPointer;
        }

        public bool IsCompatible(IDataType expected)
        {
            return expected is GenericType expectedGenericType &&
                ParameterIndex == expectedGenericType.ParameterIndex &&
                Origin == expectedGenericType.Origin;
        }

        public override string ToString()
        {
            return Identifier.Value;
        }

        public IDataType Clone(CheckedCloningInfo cloningInfo)
        {
            if (cloningInfo.TypeParameters == null || cloningInfo.TypeArguments == null)
                return new GenericType(Identifier, ParameterIndex, Origin, IsExplicitPointer);

            int typeArgumentIndex = cloningInfo.TypeParameters.FindIndex(x => x.Value == Identifier.Value);

            return cloningInfo.TypeArguments[typeArgumentIndex].Clone(cloningInfo);
        }
    }
}