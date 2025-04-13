using Caique;
using Caique.Cli;

var projectDirectory = args[0];
var diagnostics = Compilation.Compile(projectDirectory);

var diagnosticPrinter = new DiagnosticPrinter(projectDirectory);
diagnosticPrinter.Print(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Hint));
diagnosticPrinter.Print(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Warning));
diagnosticPrinter.Print(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error));
