using System;
using Caique.Ast;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique type.
    /// </summary>
    public class PrimitiveType : IDataType
    {
        public TypeKeyword Type { get; }

        public bool IsExplicitPointer { get; set; }

        public bool Allocated => false;

        public bool IsNumber { get => IsInt || IsFloat; }

        public bool IsString => false;

        public bool IsInt =>
            Type switch
            {
                TypeKeyword.i8 or
                TypeKeyword.i32 or
                TypeKeyword.i64 => true,
                _ => false,
            };

        public bool IsFloat =>
            Type switch
            {
                TypeKeyword.f8 or
                TypeKeyword.f32 or
                TypeKeyword.f64 => true,
                _ => false,
            };

        public PrimitiveType(TypeKeyword type, bool isExplicitPointer = false)
        {
            Type = type;
            IsExplicitPointer = isExplicitPointer;
        }

        /// <summary>
        /// Checks if two types are compatible with each other.
        /// </summary>
        public bool IsCompatible(IDataType expected)
        {
            return Type == expected.Type;
        }

        public override string ToString() =>
            Type.ToString().ToLower() + (IsExplicitPointer ? "*" : "");
    }
}