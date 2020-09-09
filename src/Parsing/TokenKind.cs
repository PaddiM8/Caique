namespace Caique.Parsing
{
    public enum TokenKind
    {
        // Maths
        Plus, Minus, Star, Slash, 

        // Comparison
        Equals, BangEquals, EqualsEquals, MoreOrEquals, LessOrEquals,

        // Parenthesis and brackets
        OpenParenthesis, ClosedParenthesis, OpenSquareBracket, ClosedSquareBracket, OpenAngleBracket, ClosedAngleBracket,

        // Variable length
        Identifier, NumberLiteral, StringLiteral,

        // Other
        Unknown,
    }
}