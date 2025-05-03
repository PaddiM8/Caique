namespace Caique.Cli.Handlers;

public static class BuildHandler
{
    public static void Handle(string projectPath, bool shouldDumpIr)
    {
        var options = new CompilationOptions
        {
            DumpIr = shouldDumpIr,
        };

        var diagnostics = Compilation.Compile(projectPath, options);

        var diagnosticPrinter = new DiagnosticPrinter(projectPath);
        diagnosticPrinter.Print(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Hint));
        diagnosticPrinter.Print(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Warning));
        diagnosticPrinter.Print(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error));
    }
}
