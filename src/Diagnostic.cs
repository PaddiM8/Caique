using Caique.Lexing;

namespace Caique;

public enum DiagnosticSeverity
{
    Hint,
    Warning,
    Error,
}

public record Diagnostic(DiagnosticSeverity Severity, string Message, TextSpan Span);
