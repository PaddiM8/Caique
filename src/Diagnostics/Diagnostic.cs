using Caique.Parsing;

namespace Caique.Diagnostics
{
    public class Diagnostic
    {
        public DiagnosticIdentifier Identifier { get; }

        public string Message { get; }

        public TextPosition TextPosition { get; }

        public DiagnosticType Type { get; }

        public Diagnostic(DiagnosticIdentifier identifier, string message, TextPosition textPosition, DiagnosticType type)
        {
            Identifier = identifier;
            Message = message;
            TextPosition = textPosition;
            Type = type;
        }
    }
}