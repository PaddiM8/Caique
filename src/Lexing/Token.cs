using Caique.Parsing;

namespace Caique.Lexing;

public record Token(TokenKind Kind, string Value, TextSpan Span);

public record TextSpan(TextPosition Start, TextPosition End)
{
    public TextSpan Combine(TextSpan other)
        => new(Start, other.End);
}

public record TextPosition(int Index, int Line, int Column, SyntaxTree SyntaxTree)
{
    public static TextPosition Default(SyntaxTree syntaxTree)
        => new(0, 0, 0, syntaxTree);
}

public enum TokenKind
{
    Unknown,
    Let, Var, Func, With, Class, Protocol, Module, Enum,
    Static, Override, Inheritable,
    New,
    As,
    Return, Self, Base, Default,
    If, Else, Arrow,
    Equals, Colon, ColonColon, Semicolon, Comma, Hash,
    Identifier,
    OpenParenthesis, ClosedParenthesis, OpenBracket, ClosedBracket, OpenBrace, ClosedBrace,
    Dot,
    Plus, Minus, Star, Slash,
    Pipe, Ampersand,
    PipePipe, AmpersandAmpersand,
    Exclamation, EqualsEquals, NotEquals, Greater, Less, GreaterEquals, LessEquals,
    NumberLiteral, StringLiteral, True, False,
    Comment,
    Void, Bool, I8, I16, I32, I64, I128, ISize, U8, U16, U32, U64, U128, USize, F8, F16, F32, F64, F128,
    EndOfFile,
}
