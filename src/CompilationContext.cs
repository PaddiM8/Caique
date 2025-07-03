using Caique.Scope;

namespace Caique;

public class CompilationContext(NamespaceScope preludeScope)
{
    public DiagnosticReporter DiagnosticReporter { get; } = new();

    public NamespaceScope PreludeScope { get; } = preludeScope;
}
