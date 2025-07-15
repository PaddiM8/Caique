namespace Caique.Tests.Utilities;

public record CompilationResult(IReadOnlyList<Diagnostic> Diagnostics)
{
    public IEnumerable<Diagnostic> ErrorDiagnostics
        => Diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Error);

    public CompilationResult AssertSingleCompilationError(DiagnosticCode code)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ErrorDiagnostics, Has.Exactly(1).Matches<Diagnostic>(x => x.Code == code));
        });

        return this;
    }
}
