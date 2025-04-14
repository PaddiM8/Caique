namespace Caique.Scope;

public class Project()
{
    public NamespaceScope? ProjectNamespace { get; private set; }

    public void Initialise(NamespaceScope namespaceScope)
    {
        if (ProjectNamespace != null)
            throw new InvalidOperationException("Project has already been initialised.");

        ProjectNamespace = namespaceScope;
    }

    public StructureSymbol? ResolveType(List<string> typeNames)
    {
        if (typeNames.Count == 0)
            return null;

        if (ProjectNamespace?.Name == typeNames.First())
            return ProjectNamespace.ResolveStructure(typeNames[1..]);

        // TODO: Libraries

        return null;
    }
}
