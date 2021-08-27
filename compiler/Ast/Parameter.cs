using System;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Ast
{
    public record Parameter
    {
        public Token Identifier { get; }

        public TypeExpression? Type { get; set; }

        public bool IsReference { get => Type == null; }

        public Parameter(Token identifier, TypeExpression? type)
        {
            Identifier = identifier;
            Type = type;
        }
    }
}