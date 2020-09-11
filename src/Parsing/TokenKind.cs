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

        // Keywords
        If, Else, Fn, Let, Class, Ret,

        // Variable length
        Identifier, NumberLiteral, StringLiteral,

        // Punctuation
        Dot, Comma, Colon, Semicolon,

        // Other
        Unknown, EndOfFile,
    }

    public static class TokenKindExtensions
    {
        public static int GetBinaryOperatorPrecedence(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Plus => 1,
                TokenKind.Minus => 1,
                TokenKind.Star => 2,
                TokenKind.Slash => 2,
                _ => 0,
            };
        }
    }
}