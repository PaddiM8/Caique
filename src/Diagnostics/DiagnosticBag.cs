using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Caique.Parsing;

namespace Caique.Diagnostics
{
    public class DiagnosticBag : IEnumerable<Diagnostic>
    {
        private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();

        public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void AddRange(IEnumerable<Diagnostic> diagnostics)
        {
            _diagnostics.AddRange(diagnostics);
        }

        public void ReportUnexpectedToken(Token got, string expected)
        {
            Report(
                DiagnosticIdentifier.UnexpectedToken,
                $"Unexpected token '{got.Kind.ToStringRepresentation()}', expected {expected}.",
                got.Span,
                DiagnosticType.Error
            );
        }

        public void ReportUnexpectedToken(Token got, params TokenKind[] expected)
        {
            var expectedString = new StringBuilder();

            for (int i = 0; i < expected.Length; i++)
            {
                // If last one, prepend "or" unless it's the only one
                if (i == expected.Length - 1)
                {
                    if (expected.Length >= 2)
                        expectedString.Append(" or ");
                }

                expectedString.Append($"'{expected[i].ToStringRepresentation()}'");

                // If not one of the last two
                if (i < expected.Length - 2)
                {
                    expectedString.Append(",");
                }
            }

            Report(
                DiagnosticIdentifier.UnexpectedToken,
                $"Unexpected token '{got.Kind.ToStringRepresentation()}', expected {expectedString}.",
                got.Span,
                DiagnosticType.Error
            );
        }

        public void ReportUnterminatedStringLiteral(TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnterminatedStringLiteral,
                "Unterminated string literal",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        public void ReportUnknownEscapeSequence(string escaped, TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnknownEscapeSequence,
                $"Unknown escape sequence '\\{escaped}'.",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        public void ReportUnknownToken(string tokenValue, TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnknownToken,
                $"Unknown token '{tokenValue}'.",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        private void Report(DiagnosticIdentifier identifier,
                            string message,
                            TextSpan span,
                            DiagnosticType type)
        {
            _diagnostics.Add(new Diagnostic(identifier, message, span, type));
        }
    }
}