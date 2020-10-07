using System;
using Caique.AST;
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
            switch (Type)
            {
                case TypeKeyword.i8:
                case TypeKeyword.i32:
                case TypeKeyword.i64:
                case TypeKeyword.f8:
                case TypeKeyword.f32:
                case TypeKeyword.f64:
                    return true;
                default:
                    return false;
            }
        }

        public override string ToString()
        {
            return Type == TypeKeyword.Identifier
                ? ObjectDecl!.Identifier.Value
                : Type.ToString().ToLower();
        }
    }
}