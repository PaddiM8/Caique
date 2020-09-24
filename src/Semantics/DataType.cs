using System;
using Caique.Parsing;

namespace Caique.Semantics
{
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

        public bool IsCompatible(DataType type2)
        {
            return Type == TypeKeyword.Identifier
                ? Identifier == type2.Identifier
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