using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public struct Parameter
    {
        public Token Identifier { get; }

        public TypeExpression Type { get; }

        public Parameter(Token identifier, TypeExpression type)
        {
            Identifier = identifier;
            Type = type;
        }
    }
}