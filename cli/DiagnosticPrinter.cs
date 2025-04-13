namespace Caique.Cli;

public class DiagnosticPrinter(string workingDirectory)
{
    private readonly string _workingDirectory = workingDirectory;

    public void Print(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            var path = Path.GetRelativePath(_workingDirectory, diagnostic.Span.Start.SyntaxTree.File.FilePath);
            Console.Write(path);
            Console.ResetColor();
            Console.Write(" (");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(diagnostic.Span.Start.Line);
            Console.ResetColor();
            Console.Write(":");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(diagnostic.Span.Start.Column);
            Console.ResetColor();
            Console.WriteLine(")");

            if (diagnostic.Severity == DiagnosticSeverity.Hint)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(" Hint: ");
            }
            else if (diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("  Warning: ");
            }
            else if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  Error: ");
            }

            Console.ResetColor();
            Console.WriteLine(diagnostic.Message);
        }
    }
}
