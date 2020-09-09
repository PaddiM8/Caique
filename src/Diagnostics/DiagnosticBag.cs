using System;
using System.Collections;
using System.Collections.Generic;
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

        public void ReportUnterminatedStringLiteral(TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnterminatedStringLiteral,
                "Unterminated string literal",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        public void ReportUnknownToken(string tokenValue, TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnknownToken,
                $"Unknown token '{tokenValue}'",
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