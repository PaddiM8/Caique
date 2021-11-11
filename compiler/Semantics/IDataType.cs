using System;
using Caique.Ast;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique type.
    /// </summary>
    public interface IDataType
    {
        public TypeKeyword Type { get; }

        public bool IsExplicitPointer { get; set; }

        public bool IsAllocated { get; }

        public bool IsNumber { get; }

        public bool IsString { get; }

        public bool IsFloat { get; }

        public bool IsInt { get; }

        bool IsCompatible(IDataType expected);

        string ToString();
    }
}