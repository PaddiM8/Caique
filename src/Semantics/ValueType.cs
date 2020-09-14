using System;

namespace Caique.Semantics
{
    struct ValueType
    {
        public TypeKeyword Type { get; } // TODO: Enum

        public ValueType(TypeKeyword type)
        {
            Type = type;
        }
    }
}