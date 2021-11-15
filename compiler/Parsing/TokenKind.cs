namespace Caique.Parsing
{
    public enum TokenKind
    {
        // Maths
        Plus, Minus, Star, Slash,

        // Comparison
        Equals, Bang, BangEquals, EqualsEquals, MoreOrEquals, LessOrEquals,

        // Assignment
        PlusEquals, MinusEquals, StarEquals, SlashEquals,

        // Parenthesis and brackets
        OpenParenthesis, ClosedParenthesis, OpenSquareBracket, ClosedSquareBracket,
        OpenBrace, ClosedBrace, OpenAngleBracket, ClosedAngleBracket,

        // Keywords
        If, Else, Fn, Let, Class, New, Use, Ret, Init, Deinit, Ext, Self, Super, While, True, False,
        Virtual, Override,

        // Variable length
        Identifier, NumberLiteral, StringLiteral, CharLiteral,

        // Punctuation
        Dot, Comma, Colon, Semicolon, Arrow,

        // Types
        i8, i32, i64, isize,
        f8, f32, f64,
        Bool,
        Void,

        // Other
        Unknown, EndOfFile,
    }

    public static class TokenKindExtensions
    {
        public static int GetUnaryOperatorPrecedence(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Bang => 4,
                TokenKind.Minus => 4,
                _ => 0,
            };
        }
        public static int GetBinaryOperatorPrecedence(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.EqualsEquals => 1,
                TokenKind.BangEquals => 1,
                TokenKind.MoreOrEquals => 1,
                TokenKind.LessOrEquals => 1,
                TokenKind.OpenAngleBracket => 1,
                TokenKind.ClosedAngleBracket => 1,
                TokenKind.Plus => 2,
                TokenKind.Minus => 2,
                TokenKind.Star => 3,
                TokenKind.Slash => 3,
                _ => 0,
            };
        }

        public static bool IsComparisonOperator(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.EqualsEquals or
                TokenKind.BangEquals or
                TokenKind.MoreOrEquals or
                TokenKind.LessOrEquals or
                TokenKind.OpenAngleBracket or
                TokenKind.ClosedAngleBracket => true,
                _ => false,
            };
        }

        public static bool IsAssignmentOperator(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Equals or
                TokenKind.PlusEquals or
                TokenKind.MinusEquals or
                TokenKind.StarEquals or
                TokenKind.SlashEquals => true,
                _ => false,
            };
        }

        public static bool IsKeyword(this TokenKind kind)
        {
            return kind switch
            {
                TokenKind.If or
                TokenKind.Else or
                TokenKind.Fn or
                TokenKind.Let or
                TokenKind.Class or
                TokenKind.Ret => true,
                _ => false,
            };
            ;
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
                TokenKind.PlusEquals => "+=",
                TokenKind.MinusEquals => "-=",
                TokenKind.StarEquals => "*=",
                TokenKind.SlashEquals => "/=",
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