using System;
using Caique.Parsing;

namespace Caique.Parsing
{
    public record Token(TokenKind Kind, string Value, TextSpan Span);
}