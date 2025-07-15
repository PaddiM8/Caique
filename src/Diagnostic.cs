using System.Text;
using Caique.Lexing;

namespace Caique;

public enum DiagnosticSeverity
{
    Hint,
    Warning,
    Error,
}

public record Diagnostic(DiagnosticCode Code, DiagnosticSeverity Severity, string Message, TextSpan Span)
{
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Span.Start.SyntaxTree.File.FilePath);
        builder.Append($" [{Span.Start.Line}:{Span.Start.Column}]");
        builder.Append($" {Severity}: {Message}");

        return builder.ToString();
    }
}
