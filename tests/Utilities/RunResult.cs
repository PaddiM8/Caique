namespace Caique.Tests.Utilities;

public class RunResult
{
    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }

    public required int ExitCode { get; init;}

    public IEnumerable<Diagnostic> ErrorDiagnostics
        => Diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Warning);

    public RunResult AssertNoBuildErrors()
    {
        Assert.That(ErrorDiagnostics, Is.Empty);

        return this;
    }

    public RunResult AssertSuccess()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Diagnostics.Where(x => x.Severity >= DiagnosticSeverity.Warning), Is.Empty);
            Assert.That(Stderr, Is.Empty);
            Assert.That(ExitCode, Is.EqualTo(0));
        });

        return this;
    }
}
