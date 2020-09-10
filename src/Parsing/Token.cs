using System;
using Caique.Parsing;

namespace Caique.Parsing
{
    public class Token
    {
        public TokenKind Kind { get; }

        public string Value { get; }

        public TextSpan Span { get; }

        public Token(TokenKind kind, string value, TextSpan span)
        {
            Kind = kind;
            Value = value;
            Span = span;
        }
    }
}