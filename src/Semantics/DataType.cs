using System;
using Caique.Ast;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique type.
    /// </summary>
    public struct DataType
    {
        public TypeKeyword Type { get; }

        public ClassDeclStatement? ObjectDecl { get; }

        public DataType(TypeKeyword type, ClassDeclStatement? objectDecl = null)
        {
            Type = type;
            ObjectDecl = objectDecl;
        }

        /// <summary>
        /// Checks if two types are compatible with each other.
        /// </summary>
        public bool IsCompatible(DataType expected)
        {
            if (Type == TypeKeyword.NumberLiteral && expected.IsNumber())
                return true;

            if (expected.Type == TypeKeyword.NumberLiteral && IsNumber())
                return true;

            // Objects
            if (ObjectDecl != null && expected.ObjectDecl != null)
            {
                var expectedIdentifier = expected.ObjectDecl.Identifier;
                if (ObjectDecl.HasAncestor(expectedIdentifier!.Value))
                    return true;

                return ObjectDecl?.Identifier!.Value == expectedIdentifier!.Value;
            }

            return Type == expected.Type;
        }

        public bool IsNumber()
        {
            return IsInt() || IsFloat();
        }

        public bool IsInt()
        {
            return Type switch
            {
                TypeKeyword.i8 or
                TypeKeyword.i32 or
                TypeKeyword.i64 => true,
                _ => false,
            };
        }

        public bool IsFloat()
        {
            return Type switch
            {
                TypeKeyword.f8 or
                TypeKeyword.f32 or
                TypeKeyword.f64 => true,
                _ => false,
            };
        }

        public override string ToString()
        {
            return Type == TypeKeyword.Identifier
                ? ObjectDecl!.Identifier.Value
                : Type.ToString().ToLower();
        }
    }
}