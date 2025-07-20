using System;
using System.Linq;
using System.Text;
using Caique.Parsing;

namespace Caique.Lexing;

public class Lexer
{
    private char? Current
        => _input.ElementAtOrDefault(_index);

    private bool ReachedEnd
        => _index >= _input.Length;

    private readonly string _input;
    private readonly SyntaxTree _syntaxTree;
    private readonly DiagnosticReporter _diagnostics;
    private int _index;
    private int _line;
    private int _column;

    private Lexer(string input, SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        _input = input;
        _syntaxTree = syntaxTree;
        _diagnostics = compilationContext.DiagnosticReporter;
    }

    public static IEnumerable<Token> Lex(string input, SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        var lexer = new Lexer(input, syntaxTree, compilationContext);
        while (!lexer.ReachedEnd)
        {
            var token = lexer.Next();
            yield return token;

            if (token.Kind == TokenKind.EndOfFile)
                yield break;
        }

        var endPosition = lexer.GetTextPosition();
        var endSpan = new TextSpan(endPosition, endPosition);

        yield return new Token(TokenKind.EndOfFile, string.Empty, endSpan);
    }

    private Token Next()
    {
        while (Match(' ', '\t', '\n', '\r'))
            Eat();

        if (ReachedEnd)
        {
            var endPosition = GetTextPosition();
            var endSpan = new TextSpan(endPosition, endPosition);

            return new Token(TokenKind.EndOfFile, string.Empty, endSpan);
        }

        var start = GetTextPosition();
        var (kind, content) = Current switch
        {
            '+' => (TokenKind.Plus, Eat()),
            '-' => Peek() == '>'
                ? (TokenKind.Arrow, Eat(2))
                : (TokenKind.Minus, Eat()),
            '*' => (TokenKind.Star, Eat()),
            '/' => (TokenKind.Slash, Eat()),
            '&' => Peek() == '&'
                ? (TokenKind.AmpersandAmpersand, Eat(2))
                : (TokenKind.Ampersand, Eat()),
            '|' => Peek() == '|'
                ? (TokenKind.PipePipe, Eat(2))
                : (TokenKind.Pipe, Eat()),
            '!' => Peek() == '='
                ? (TokenKind.NotEquals, Eat(2))
                : (TokenKind.Exclamation, Eat()),
            '=' => Peek() == '='
                ? (TokenKind.EqualsEquals, Eat(2))
                : (TokenKind.Equals, Eat()),
            '>' => Peek() == '='
                ? (TokenKind.GreaterEquals, Eat(2))
                : (TokenKind.Greater, Eat()),
            '<' => Peek() == '='
                ? (TokenKind.LessEquals, Eat(2))
                : (TokenKind.Less, Eat()),
            '(' => (TokenKind.OpenParenthesis, Eat()),
            ')' => (TokenKind.ClosedParenthesis, Eat()),
            '[' => (TokenKind.OpenBracket, Eat()),
            ']' => (TokenKind.ClosedBracket, Eat()),
            '{' => (TokenKind.OpenBrace, Eat()),
            '}' => (TokenKind.ClosedBrace, Eat()),
            ':' => Peek() == ':'
                ? (TokenKind.ColonColon, Eat(2))
                : (TokenKind.Colon, Eat()),
            ';' => (TokenKind.Semicolon, Eat()),
            ',' => (TokenKind.Comma, Eat()),
            '#' => (TokenKind.Hash, Eat()),
            '.' => (TokenKind.Dot, Eat()),
            _ => (TokenKind.Unknown, ""),
        };

        if (kind == TokenKind.Unknown)
        {
            return NextComplex(start);
        }

        var end = GetTextPosition();

        return new Token(kind, content, new TextSpan(start, end));
    }

    private Token NextComplex(TextPosition start)
    {
        if (char.IsLetter(Current!.Value) || Current == '_')
        {
            return NextIdentifierOrKeyword(start);
        }

        if (Current == '"')
        {
            return NextString(start);
        }

        if (char.IsDigit(Current!.Value))
        {
            return NextNumber(start);
        }

        var c = Eat();
        var end = GetTextPosition();

        return new Token(TokenKind.Unknown, c, new TextSpan(start, end));
    }

    private Token NextIdentifierOrKeyword(TextPosition start)
    {
        var builder = new StringBuilder();
        builder.Append(Eat());
        while (!ReachedEnd && (char.IsLetterOrDigit(Current!.Value) || Current == '_'))
        {
            builder.Append(Eat());
        }

        var text = builder.ToString();
        var kind = text switch
        {
            "with" => TokenKind.With,
            "let" => TokenKind.Let,
            "var" => TokenKind.Var,
            "func" => TokenKind.Func,
            "class" => TokenKind.Class,
            "protocol" => TokenKind.Protocol,
            "module" => TokenKind.Module,
            "enum" => TokenKind.Enum,
            "inheritable" => TokenKind.Inheritable,
            "pub" => TokenKind.Pub,
            "static" => TokenKind.Static,
            "override" => TokenKind.Override,
            "new" => TokenKind.New,
            "as" => TokenKind.As,
            "return" => TokenKind.Return,
            "base" => TokenKind.Base,
            "default" => TokenKind.Default,
            "if" => TokenKind.If,
            "else" => TokenKind.Else,
            "true" => TokenKind.True,
            "false" => TokenKind.False,
            "void" => TokenKind.Void,
            "bool" => TokenKind.Bool,
            "i8" => TokenKind.I8,
            "i16" => TokenKind.I16,
            "i32" => TokenKind.I32,
            "i64" => TokenKind.I64,
            "i128" => TokenKind.I128,
            "u8" => TokenKind.U8,
            "u16" => TokenKind.U16,
            "u32" => TokenKind.U32,
            "u64" => TokenKind.U64,
            "u128" => TokenKind.U128,
            "f8" => TokenKind.F8,
            "f16" => TokenKind.F16,
            "f32" => TokenKind.F32,
            "f64" => TokenKind.F64,
            "f128" => TokenKind.F128,
            "isize" => TokenKind.ISize,
            "usize" => TokenKind.USize,
            _ => TokenKind.Identifier,
        };

        var end = GetTextPosition();
        var value = kind == TokenKind.Identifier
            ? text
            : string.Empty;

        return new Token(kind, value, new TextSpan(start, end));
    }

    private Token NextString(TextPosition start)
    {
        var builder = new StringBuilder();
        Eat();

        while (!ReachedEnd && !Match('"'))
        {
            if (Current != '\\')
            {
                builder.Append(Eat());

                continue;
            }

            Eat();
            if (ReachedEnd)
                continue;

            var next = Eat();
            var nextToAppend = next switch
            {
                "n" => "\n",
                "t" => "\t",
                "0" => "\0",
                _ => next,
            };

            builder.Append(nextToAppend);
        }

        AdvanceIf('"');
        var end = GetTextPosition();

        return new Token(
            TokenKind.StringLiteral,
            builder.ToString(),
            new TextSpan(start, end)
        );
    }

    private Token NextNumber(TextPosition start)
    {
        var builder = new StringBuilder();
        builder.Append(Eat());

        while (!ReachedEnd && (char.IsDigit(Current!.Value) || Current == '.'))
        {
            // If the dot isn't followed by a digit, the dot should be lexed
            // as a separate token, to allow things like `2.as(i64)`.
            var peek = Peek();
            if (Current == '.' && peek.HasValue && !char.IsDigit(peek.Value))
                break;

            builder.Append(Eat());
        }

        var text = builder.ToString();
        var end = GetTextPosition();

        if (text.Count(c => c == '.') > 1)
        {
            _diagnostics.ReportUnrecognisedToken(text, new TextSpan(start, end));
        }

        return new Token(
            TokenKind.NumberLiteral,
            text,
            new TextSpan(start, end)
        );
    }

    private bool Match(params char[] chars)
        => Current.HasValue && chars.Contains(Current.Value);

    private bool AdvanceIf(char c)
    {
        if (Match(c))
        {
            Eat();

            return true;
        }

        return false;
    }

    private char? Peek(int n = 1)
    {
        return _input.ElementAtOrDefault(_index + n);
    }

    private string Eat(int count = 1)
    {
        var consumed = _input[_index..(_index + count)];
        foreach (var consumedChar in consumed)
        {
            _index++;

            if (consumedChar == '\n')
            {
                _line++;
                _column = 0;
            }
            else
            {
                _column++;
            }
        }

        return consumed;
    }

    private TextPosition GetTextPosition()
        => new(_index, _line, _column, _syntaxTree);
}
