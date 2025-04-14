using System.Diagnostics;
using Caique.Analysis;
using Caique.Parsing;
using Caique.Preprocessing;
using Caique.Resolving;

namespace Caique;

public class Compilation
{
    public static IEnumerable<Diagnostic> Compile(string projectFilePath)
    {
        // TODO: The actual project name should be defined in the project file
        // var directoryPath = Path.GetDirectoryName(projectFilePath)!;
        var project = Preprocessor.Process("root", projectFilePath);
        var context = new CompilationContext();
        project.ProjectNamespace!.Traverse(scope =>
        {
            var content = File.ReadAllText(scope.FilePath);
            scope.SyntaxTree = Parser.Parse(content, scope, context);
        });

        if (!context.DiagnosticReporter.Errors.Any())
        {
            project.ProjectNamespace.Traverse(scope =>
            {
                Debug.Assert(scope.SyntaxTree != null);
                Resolver.Resolve(scope.SyntaxTree, context);
            });
        }

        if (!context.DiagnosticReporter.Errors.Any())
        {
            project.ProjectNamespace.Traverse(scope =>
            {
                Debug.Assert(scope.SyntaxTree != null);
                Analyser.Analyse(scope.SyntaxTree, context);
            });
        }

        return context.DiagnosticReporter.Diagnostics;
    }
}
