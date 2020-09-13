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
                TokenKind.EqualsEquals => 1,
                TokenKind.BangEquals => 1,
                TokenKind.MoreOrEquals => 1,
                TokenKind.LessOrEquals => 1,
                TokenKind.Plus => 2,
                TokenKind.Minus => 2,
                TokenKind.Star => 3,
                TokenKind.Slash => 3,
                _ => 0,
            };
        }

        public static bool IsKeyword(this TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.If:
                case TokenKind.Else:
                case TokenKind.Fn:
                case TokenKind.Let:
                case TokenKind.Class:
                case TokenKind.Ret:
                    return true;
                default:
                    return false;
            };
        }

        public static string ToStringRepresentation(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Plus => "+",
                TokenKind.Minus => "-",
                TokenKind.Star => "*",
                TokenKind.Slash => "/",
                TokenKind.Equals => "=",
                TokenKind.Bang => "!",
                TokenKind.BangEquals => "!=",
                TokenKind.EqualsEquals => "==",
                TokenKind.MoreOrEquals => ">=",
                TokenKind.LessOrEquals => "<=",
                TokenKind.OpenParenthesis => "(",
                TokenKind.ClosedParenthesis => ")",
                TokenKind.OpenSquareBracket => "[",
                TokenKind.ClosedSquareBracket => "]",
                TokenKind.OpenBrace => "{",
                TokenKind.ClosedBrace => "}",
                TokenKind.OpenAngleBracket => "<",
                TokenKind.ClosedAngleBracket => ">",
                TokenKind.Dot => ".",
                TokenKind.Comma => ",",
                TokenKind.Colon => ":",
                TokenKind.Semicolon => ";",
                _ => kind.ToString().ToLower(),
            };
        }
    }
}