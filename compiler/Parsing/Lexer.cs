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
        /// <summary>
        /// Whether or not the index is outside the bounds of the source.
        /// </summary>
        private bool IsAtEnd => _position.index >= _source.Length;

        private char Current
        {
            get
            {
                if (IsAtEnd) return '\0';

                return _source[_position.index];
            }
        }

        private char Previous
        {
            get
            {
                if (_position.index == 0) return '\0';

                return _source[_position.index - 1];
            }
        }

        /// <summary>
        /// The character in front of the current one.
        /// </summary>
        /// <value></value>
        private char Lookahead
        {
            get
            {
                if (_position.index + 1 >= _source.Length) return '\0';

                return _source[_position.index + 1];
            }
        }

        private TextPosition CurrentTextPosition => new(_position.line, _position.column);

        private readonly string _source;
        private (int index, int line, int column) _position = (0, 1, 1);
        private readonly DiagnosticBag _diagnostics;

        public Lexer(string source, DiagnosticBag diagnostics)
        {
            _source = source;
            _diagnostics = diagnostics;
        }

        /// <summary>
        /// Start the lexing process.
        /// </summary>
        public static List<Token> Lex(string source, DiagnosticBag diagnostics)
        {
            var lexer = new Lexer(source, diagnostics);
            var tokens = new List<Token>();
            while (!lexer.IsAtEnd)
            {
                tokens.Add(lexer.NextToken());
            }

            // Make sure there is an EndOfFile token.
            if (tokens.Last().Kind != TokenKind.EndOfFile)
            {
                tokens.Add(new Token(TokenKind.EndOfFile, "", new TextSpan(
                    lexer.CurrentTextPosition,
                    lexer.CurrentTextPosition)
                ));
            }

            return tokens;
        }

        private Token NextToken()
        {
            // Ignore whitespace and comments
            while (!IsAtEnd)
            {
                // Whitespace
                if (char.IsWhiteSpace(Current))
                {
                    if (Current == '\n') NextLine();
                    Advance();

                    continue;
                }

                // Single-line comments
                if (Current == '/' && Lookahead == '/')
                {
                    while (!IsAtEnd && Current != '\n')
                        Advance();

                    continue;
                }

                // Multi-line comments
                if (Current == '/' && Lookahead == '*')
                {
                    while (!IsAtEnd)
                    {
                        if (Current == '*' && Lookahead == '/') break;
                        if (Current == '\n') NextLine();
                        Advance();
                    }

                    // Advance past the final * and /
                    Advance();
                    Advance();

                    continue;
                }

                break;
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
                    else if (Current == '\'')
                    {
                        value = NextCharLiteral();
                        kind = TokenKind.CharLiteral;
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

            // If it ended with a dot, the dot wasn't a part of the number,
            // so don't include that.
            if (Previous == '.')
            {
                Retreat();
                length--;
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
                    value.Append(NextEscapeSequence());
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

        private string NextCharLiteral()
        {
            Advance(); // '
            char value = Current == '\\'
                ? NextEscapeSequence()
                : Current;

            Advance();

            if (Current != '\'')
            {
                _diagnostics.ReportInvalidCharacterLiteral(CurrentTextPosition);
            }

            return value.ToString();
        }

        private char NextEscapeSequence()
        {
            Advance(); // \
            char? escaped = Current switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '0' => '\0',
                '"' => '"',
                '\\' => '\\',
                _ => null,
            };

            if (escaped == null)
            {
                _diagnostics.ReportUnknownEscapeSequence(
                    Current.ToString(),
                    CurrentTextPosition
                );

                return '\0';
            }

            return escaped!.Value;
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
                "init" => TokenKind.Init,
                "ext" => TokenKind.Ext,
                "self" => TokenKind.Self,
                "new" => TokenKind.New,
                "use" => TokenKind.Use,
                "while" => TokenKind.While,
                "void" => TokenKind.Void,
                "i8" => TokenKind.i8,
                "i32" => TokenKind.i32,
                "i64" => TokenKind.i64,
                "f8" => TokenKind.f8,
                "f32" => TokenKind.f32,
                "f64" => TokenKind.f64,
                "true" => TokenKind.True,
                "false" => TokenKind.False,
                _ => TokenKind.Identifier,
            };

            return (value, kind);
        }

        /// <summary>
        /// Increment the variable that keeps track of the line,
        /// and reset the variable that keeps track of the column
        /// </summary>
        private void NextLine()
        {
            _position.line++;
            _position.column = 0;
        }

        /// <summary>
        /// Move on to the next character.
        /// </summary>
        private void Advance()
        {
            if (IsAtEnd) return;

            _position.index++;
            _position.column++;
        }

        /// <summary>
        /// Go back to the previous character.
        /// </summary>
        private void Retreat()
        {
            _position.index--;
            _position.column--;
        }
    }
}