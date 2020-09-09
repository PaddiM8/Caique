namespace Caique.Parsing
{
    public enum TokenKind
    {
        // Maths
        Plus, Minus, Star, Slash,

        // Comparison
        Equals, Bang, BangEquals, EqualsEquals, MoreOrEquals, LessOrEquals,

        // Parenthesis and brackets
        OpenParenthesis, ClosedParenthesis, OpenSquareBracket, ClosedSquareBracket,
        OpenBrace, ClosedBrace, OpenAngleBracket, ClosedAngleBracket,

        // Variable length
        Identifier, NumberLiteral, StringLiteral,

        // Other
        Unknown, EndOfFile,
    }
}