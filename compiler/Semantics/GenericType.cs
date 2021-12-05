using System;
using System.Linq;
using Caique.Ast;
using Caique.CheckedTree;
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

        public IGenericTypeOrigin Origin { get; }

        public bool IsExplicitPointer { get; set; }

        public bool IsAllocated => false;

        public bool IsNumber => false;

        public bool IsString => false;

        public bool IsFloat => false;

        public bool IsInt => false;

        public GenericType(Token identifier,
                           int parameterIndex,
                           IGenericTypeOrigin origin,
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
            int typeArgumentIndex = cloningInfo.TypeParameters?.FindIndex(x => x.Value == Identifier.Value) ?? -1;
            if (typeArgumentIndex == -1 || cloningInfo.TypeArguments == null || !cloningInfo.TypeArguments.Any())
                return new GenericType(Identifier, ParameterIndex, Origin, IsExplicitPointer);

            return cloningInfo.TypeArguments[typeArgumentIndex].Clone(cloningInfo);
        }
    }
}