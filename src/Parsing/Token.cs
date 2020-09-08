using System;
using Caique.Parsing;

namespace Caique.Parsing
{
    class Token
    {
        public TokenKind Kind { get; }

        public string Value { get; }

        public (TextPosition from, TextPosition to) Span { get; }

        public Token(TokenKind kind, string value, (TextPosition from, TextPosition to) span)
        {
            Kind = kind;
            Value = value;
            Span = span;
        }
    }
}