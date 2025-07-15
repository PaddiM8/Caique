namespace Caique.Tests.Utilities;

public record CompilationResult(IReadOnlyList<Diagnostic> Diagnostics)
{
    public IEnumerable<Diagnostic> ErrorDiagnostics
        => Diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Error);
}
