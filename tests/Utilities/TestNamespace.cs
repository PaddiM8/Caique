using Caique.Scope;

namespace Caique.Tests.Utilities;

public class TestNamespace
{
    private readonly string _path;
    private readonly Project _project;
    private readonly NamespaceScope _namespace;

    public TestNamespace(string name, Project project, IScope? parent)
    {
        _path = Path.Combine(parent?.Namespace.FilePath ?? project.ProjectFilePath, name);
        _project = project;
        _namespace = new NamespaceScope(name, _path, parent, project);
    }

    public NamespaceScope Build()
        => _namespace;

    public TestNamespace AddNamespace(string name, Func<TestNamespace, TestNamespace> callback)
    {
        var testNamespace = new TestNamespace(name, _project, _namespace);
        var scope = callback(testNamespace).Build();
        _namespace.AddScope(scope);

        return this;
    }

    public TestNamespace AddFile(string name, string content)
    {
        var filePath = Path.Combine(_path, $"{name}.cq");
        var file = new FileScope(name, filePath, content, _namespace);
        _namespace.AddScope(file);

        return this;
    }
}
