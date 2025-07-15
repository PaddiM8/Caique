using Caique.Scope;

namespace Caique;

public class CompilationContext(NamespaceScope stdScope)
{
    public DiagnosticReporter DiagnosticReporter { get; } = new();

    public NamespaceScope StdScope { get; } = stdScope;
}
