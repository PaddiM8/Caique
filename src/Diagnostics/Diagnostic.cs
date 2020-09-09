using Caique.Parsing;

namespace Caique.Diagnostics
{
    public class Diagnostic
    {
        public DiagnosticIdentifier Identifier { get; }

        public string Message { get; }

        public TextSpan Span { get; }

        public DiagnosticType Type { get; }

        public Diagnostic(DiagnosticIdentifier identifier, string message, TextSpan span, DiagnosticType type)
        {
            Identifier = identifier;
            Message = message;
            Span = span;
            Type = type;
        }
    }
}