namespace Caique.Scope;

public interface IScope
{
    IScope? Parent { get; }

    NamespaceScope Namespace { get; }
}
