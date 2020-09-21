using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Caique.Diagnostics;
using Caique.Parsing;

namespace Caique.Parsing
{
    class Lexer
    {
        private bool IsAtEnd
        {
            get
            {
                return _position.index >= _source.Length;
            }
        }

        private char Current
        {
            get
            {
                if (IsAtEnd) return '\0';

                return _source[_position.index];
            }
        }

        private char Lookahead
        {
            get
            {
                if (_position.index + 1 >= _source.Length) return '\0';

                return _source[_position.index + 1];
            }
        }

        private TextPosition CurrentTextPosition
        {
            get
            {
                return new TextPosition(_position.line, _position.column);
            }
        }

        private readonly string _source;
        private (int index, int line, int column) _position = (0, 1, 1);
        private readonly DiagnosticBag _diagnostics;

        public Lexer(string source, DiagnosticBag diagnostics)
        {
            _source = source;
            _diagnostics = diagnostics;
        }

        public List<Token> Lex()
        {
            var tokens = new List<Token>();
            while (!IsAtEnd)
            {
                tokens.Add(NextToken());
            }

            // Make sure there is an EndOfFile token.
            if (tokens.Last().Kind != TokenKind.EndOfFile)
            {
                tokens.Add(new Token(TokenKind.EndOfFile, "", new TextSpan(
                    CurrentTextPosition,
                    CurrentTextPosition)
                ));
            }

            return tokens;
        }

        private Token NextToken()
        {
            // Ignore any whitespace
            while (!IsAtEnd && char.IsWhiteSpace(Current))
            {
                if (Current == '\n')
                {
                    _position.line++;
                    _position.column = 0;
                }

                Advance();
            }


            // If the last character is whitespace,
            // something will still be needed to be returned.
            // Return an end of file token.
            if (IsAtEnd)
            {
                return new Token(TokenKind.EndOfFile, "", new TextSpan(
                    CurrentTextPosition,
                    CurrentTextPosition)
                );
            }

            TokenKind kind = TokenKind.Unknown;
            TextPosition startPos = CurrentTextPosition;
            string value = "";

            switch (Current)
            {
                case '+':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.PlusEquals;
                        Advance();
                    }
                    else kind = TokenKind.Plus;
                    break;
                case '-':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.MinusEquals;
                        Advance();
                    }
                    else if (Lookahead == '>')
                    {
                        kind = TokenKind.Arrow;
                        Advance();
                    }
                    else kind = TokenKind.Minus;
                    break;
                case '*':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.StarEquals;
                        Advance();
                    }
                    else kind = TokenKind.Star;
                    break;
                case '/':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.SlashEquals;
                        Advance();
                    }
                    else kind = TokenKind.Slash;
                    break;
                case '(': kind = TokenKind.OpenParenthesis; break;
                case ')': kind = TokenKind.ClosedParenthesis; break;
                case '[': kind = TokenKind.OpenSquareBracket; break;
                case ']': kind = TokenKind.ClosedSquareBracket; break;
                case '{': kind = TokenKind.OpenBrace; break;
                case '}': kind = TokenKind.ClosedBrace; break;
                case '.': kind = TokenKind.Dot; break;
                case ',': kind = TokenKind.Comma; break;
                case ':': kind = TokenKind.Colon; break;
                case ';': kind = TokenKind.Semicolon; break;
                case '=':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.EqualsEquals;
                        Advance();
                    }
                    else kind = TokenKind.Equals;
                    break;
                case '!':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.BangEquals;
                        Advance();
                    }
                    else kind = TokenKind.Bang;
                    break;
                case '>':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.MoreOrEquals;
                        Advance();
                    }
                    else kind = TokenKind.ClosedAngleBracket;
                    break;
                case '<':
                    if (Lookahead == '=')
                    {
                        kind = TokenKind.LessOrEquals;
                        Advance();
                    }
                    else kind = TokenKind.OpenAngleBracket;
                    break;
                default:
                    if (char.IsDigit(Current))
                    {
                        value = NextNumberLiteral();
                        kind = TokenKind.NumberLiteral;
                    }
                    else if (Current == '"')
                    {
                        value = NextStringLiteral();
                        kind = TokenKind.StringLiteral;
                    }
                    else if (char.IsLetterOrDigit(Current) || Current == '_')
                    {
                        (value, kind) = NextIdentifier();
                    }
                    else
                    {
                        _diagnostics.ReportUnknownToken(
                            Current.ToString(),
                            CurrentTextPosition
                        );
                    }
                    break;
            }

            TextPosition endPos = CurrentTextPosition;
            Advance();

            return new Token(kind, value, new TextSpan(startPos, endPos));
        }

        private string NextNumberLiteral()
        {
            int start = _position.index;
            int length = 0;

            while (char.IsDigit(Current) || Current == '.')
            {
                Advance();
                length++;
            }

            // The position should be at the last character in the literal
            // before returning. This is to make sure the eventual token
            // will have a correct TextSpan.
            Retreat();

            return _source.Substring(start, length);
        }

        private string NextStringLiteral()
        {
            Advance(); // Skip past the first "
            var value = new StringBuilder();

            while (Current != '"' && Current != '\n')
            {
                if (Current == '\\')
                {
                    if (IsAtEnd) break;
                    Advance();

                    char? escaped = Current switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        '"' => '"',
                        '\\' => '\\',
                        _ => null,
                    };

                    if (escaped != null)
                    {
                        value.Append(escaped);
                    }
                    else
                    {
                        _diagnostics.ReportUnknownEscapeSequence(
                            Current.ToString(),
                            CurrentTextPosition
                        );
                    }

                    Advance();

                    continue;
                }

                value.Append(Current);
                Advance();
            }

            // If the loop didn't break because of
            // a double quote, the string literal was never terminated,
            // so throw an error.
            if (Current != '"')
            {
                _diagnostics.ReportUnterminatedStringLiteral(CurrentTextPosition);
            }

            return value.ToString();
        }

        private (string, TokenKind) NextIdentifier()
        {
            int start = _position.index;
            int length = 0;

            while (char.IsLetterOrDigit(Current) || Current == '_')
            {
                Advance();
                length++;
            }

            // The position should be at the last character in the literal
            // before returning. This is to make sure the eventual token
            // will have a correct TextSpan.
            Retreat();

            // Figure out which (if any) keyword it is.
            // If it is not any keyword, it's just an identifier.
            string value = _source.Substring(start, length);
            var kind = value switch
            {
                "if" => TokenKind.If,
                "else" => TokenKind.Else,
                "fn" => TokenKind.Fn,
                "ret" => TokenKind.Ret,
                "let" => TokenKind.Let,
                "class" => TokenKind.Class,
                "new" => TokenKind.New,
                "use" => TokenKind.Use,
                "void" => TokenKind.Void,
                "i8" => TokenKind.i8,
                "i32" => TokenKind.i32,
                "i64" => TokenKind.i64,
                "f8" => TokenKind.f8,
                "f32" => TokenKind.f32,
                "f64" => TokenKind.f64,
                _ => TokenKind.Identifier,
            };

            return (value, kind);
        }

        private void Advance()
        {
            if (IsAtEnd) return;

            _position.index++;
            _position.column++;
        }

        private void Retreat()
        {
            _position.index--;
            _position.column--;
        }
    }
}