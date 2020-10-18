using System;
using Caique.Parsing;

namespace Caique.Ast
{
    public record Parameter(Token Identifier, TypeExpression Type);
}