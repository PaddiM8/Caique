using System;
using Xunit;
using Caique.Parsing;
using System.Collections.Generic;
using Caique.Diagnostics;

namespace Caique.Tests
{
    public class LexerTests
    {
        private readonly DiagnosticBag _diagnostics;

        public LexerTests()
        {
            _diagnostics = new DiagnosticBag();
        }

        [Theory]
        [MemberData(nameof(GetTokensData))]
        public void Lexer_Lexes_Token(TokenKind kind, string text)
        {
            var tokens = Lex(text);

            var token = Assert.Single(tokens);
            Assert.Equal(kind, token.Kind);

            if (kind == TokenKind.NumberLiteral ||
                kind == TokenKind.Identifier)
            {
                Assert.Equal(text, token.Value);
            }
            else if (kind == TokenKind.StringLiteral)
            {
                Assert.Equal(text, $"\"token.Value\"");
            }
        }

        [Fact]
        public void Lexer_UnterminatedStringLiteral_ReturnsTokenOnNextLineAndError()
        {
            string source = "\"abc\n123";
            var tokens = Lex(source);
            Assert.Equal(2, tokens.Count);
            Assert.Equal(TokenKind.NumberLiteral, tokens[1].Kind);
            Assert.Equal("123", tokens[1].Value);

            var error = Assert.Single(_diagnostics);
            Assert.Equal(DiagnosticIdentifier.UnterminatedStringLiteral, error.Identifier);
        }

        [Theory]
        [InlineData("\\n", "\n")]
        [InlineData("\\t", "\t")]
        [InlineData("\\r", "\r")]
        [InlineData("\\\"", "\"")]
        [InlineData("\\\\", "\\")]
        public void Lexer_StringLiteralWithEscapeSequences_InsertTheSequences(string inputSequence, string expected)
        {
            var tokens = Lex($"\"{inputSequence}\"");
            var token = Assert.Single(tokens);
            Assert.Equal(TokenKind.StringLiteral, token.Kind);
            Assert.Equal(expected, token.Value);
        }

        [Theory]
        [MemberData(nameof(GetTokenPairsData))]
        public void Lexer_TokenPairs_WithAccurateTextPosition((TokenKind kind, string text) token1, (TokenKind kind, string text) token2)
        {
            var tokens = Lex(token1.text + " " + token2.text);

            // Token 1 start pos
            Assert.Equal(
                new TextPosition(1, 1),
                tokens[0].Span.Start
            );

            // Token 1 end pos
            Assert.Equal(
                new TextPosition(1, token1.text.Length),
                tokens[0].Span.End
            );

            // Token 2 start pos 
            Assert.Equal(
                new TextPosition(1, token1.text.Length + 2), // Account for spaces
                tokens[1].Span.Start
            );

            // Token 2 end pos
            Assert.Equal(
                new TextPosition(1, token1.text.Length + 1 + token2.text.Length), // Account for spaces
                tokens[1].Span.End
            );
        }


        [Theory]
        [InlineData(" ")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\r")]
        [InlineData("\n")]
        [InlineData("\n\r\t ")]
        public void Lexer_WithWhitespace_IgnoresWhitespace(string whitespace)
        {
            var tokens = Lex("123" + whitespace + "abc");
            Assert.Equal(2, tokens.Count);
            Assert.Equal("123", tokens[0].Value);
            Assert.Equal(TokenKind.NumberLiteral, tokens[0].Kind);
            Assert.Equal("abc", tokens[1].Value);
            Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
        }

        private List<Token> Lex(string source)
        {
            return new Lexer(source, _diagnostics).Lex();
        }

        public static IEnumerable<object[]> GetTokensData()
        {
            foreach (var (kind, text) in GetTokens())
                yield return new object[] { kind, text };
        }

        public static IEnumerable<object[]> GetTokenPairsData()
        {
            foreach (var token1 in GetTokens())
            {
                foreach (var token2 in GetTokens())
                {
                    yield return new object[] { token1, token2 };
                }
            }
        }

        private static IEnumerable<(TokenKind kind, string text)> GetTokens()
        {
            return new[]
            {
                (TokenKind.Plus, "+"),
                (TokenKind.Minus, "-"),
                (TokenKind.Slash, "/"),
                (TokenKind.Equals, "="),
                (TokenKind.Bang, "!"),
                (TokenKind.BangEquals, "!="),
                (TokenKind.EqualsEquals, "=="),
                (TokenKind.MoreOrEquals, ">="),
                (TokenKind.LessOrEquals, "<="),
                (TokenKind.OpenParenthesis, "("),
                (TokenKind.ClosedParenthesis, ")"),
                (TokenKind.OpenSquareBracket, "["),
                (TokenKind.ClosedSquareBracket, "]"),
                (TokenKind.OpenBrace, "{"),
                (TokenKind.ClosedBrace, "}"),
                (TokenKind.OpenAngleBracket, "<"),
                (TokenKind.ClosedAngleBracket, ">"),

                (TokenKind.Identifier, "a"),
                (TokenKind.Identifier, "_abc"),
                (TokenKind.Identifier, "a_bc"),
                (TokenKind.NumberLiteral, "1"),
                (TokenKind.NumberLiteral, "123"),
                (TokenKind.NumberLiteral, "1.23"),
            };
        }
    }
}
