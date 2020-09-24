using System;
using Caique.Parsing;

namespace Caique.Semantics
{
    /// <summary>
    /// Represents a Caique type.
    /// </summary>
    public struct DataType
    {
        public TypeKeyword Type { get; }

        public Token? Identifier { get; }

        public ModuleEnvironment? Module { get; }

        public DataType(TypeKeyword type, Token? identifier = null,
                        ModuleEnvironment? module = null)
        {
            Type = type;
            Identifier = identifier;
            Module = module;
        }

        /// <summary>
        /// Checks if two types are compatible with each other.
        /// </summary>
        public bool IsCompatible(DataType type2)
        {
            if (Type == TypeKeyword.NumberLiteral && type2.IsNumber())
                return true;

            if (type2.Type == TypeKeyword.NumberLiteral && IsNumber())
                return true;

            return Type == TypeKeyword.Identifier
                ? Identifier!.Value == type2.Identifier!.Value
                : Type == type2.Type;
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
                ? Identifier!.Value
                : Type.ToString().ToLower();
        }
    }
}