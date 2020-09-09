using System;
using System.Collections.Generic;
using System.Linq;
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

        private char Previous
        {
            get
            {
                return _source[_position.index - 1];
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

            if (tokens.Last().Kind == TokenKind.EndOfFile)
            {
                tokens.RemoveAt(tokens.Count - 1);
            }

            return tokens;
        }

        private Token NextToken()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
            {
                Advance();

                if (Current == '\n')
                {
                    _position.line++;
                    _position.column = 0;
                }
            }


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
                case '+': kind = TokenKind.Plus; break;
                case '-': kind = TokenKind.Minus; break;
                case '*': kind = TokenKind.Star; break;
                case '/': kind = TokenKind.Slash; break;
                case '(': kind = TokenKind.OpenParenthesis; break;
                case ')': kind = TokenKind.ClosedParenthesis; break;
                case '[': kind = TokenKind.OpenSquareBracket; break;
                case ']': kind = TokenKind.ClosedSquareBracket; break;
                case '{': kind = TokenKind.OpenBrace; break;
                case '}': kind = TokenKind.ClosedBrace; break;
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

            Retreat();

            return _source.Substring(start, length);
        }

        private string NextStringLiteral()
        {
            Advance(); // Skip past the first "

            int start = _position.index;
            int length = 0;

            while (Current != '"' && Current != '\n')
            {
                Advance();
                length++;
            }

            if (Previous != '"')
            {
                _diagnostics.ReportUnterminatedStringLiteral(CurrentTextPosition);
            }
            return _source.Substring(start, length);
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

            Retreat();

            // This will later return keywords as well,
            // meaning it needs to return the TokenKind.
            return (_source.Substring(start, length), TokenKind.Identifier);
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