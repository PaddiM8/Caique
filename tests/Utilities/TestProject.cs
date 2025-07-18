using System.Diagnostics;
using System.Text;
using Caique.Scope;

namespace Caique.Tests.Utilities;

public class TestProject
{
    private readonly string _name;
    private readonly string _path;
    private readonly Project _project;
    private readonly CompilationOptions _compilationOptions = new()
    {
        DumpIr = true,
    };

    private TestProject(string name, string path, Project project)
    {
        _name = name;
        _path = path;
        _project = project;
    }

    public static TestProject Create()
    {
        var name = TestContext.CurrentContext.Test.Name;
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", name);
        Directory.CreateDirectory(path);

        var project = new Project(name, path);
        var projectNamespace = new NamespaceScope(name, path, parent: null, project);
        project.Initialise(projectNamespace);

        return new TestProject(name, path, project);
    }

    public CompilationResult Compile()
    {
        var diagnostics = Compilation.Compile(_project, _compilationOptions);

        return new CompilationResult(diagnostics);
    }

    public RunResult Run()
    {
        var diagnostics = Compilation.Compile(_project, _compilationOptions);
        if (diagnostics.Any(x => x.Severity >= DiagnosticSeverity.Error))
        {
            return new RunResult
            {
                Diagnostics = diagnostics,
                Stdout = string.Empty,
                Stderr = string.Empty,
                ExitCode = 0,
            };
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(_path, "target", _name),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        process.Start();
        process.WaitForExit();

        return new RunResult
        {
            Diagnostics = diagnostics,
            Stdout = process.StandardOutput.ReadToEnd(),
            Stderr = process.StandardError.ReadToEnd(),
            ExitCode = process.ExitCode,
        };
    }

    public TestProject AddNamespace(string name, Func<TestNamespace, TestNamespace> callback)
    {
        var testNamespace = new TestNamespace(name, _project, parent: null);
        var scope = callback(testNamespace).Build();
        _project.ProjectNamespace!.AddScope(scope);

        return this;
    }

    public TestProject AddFile(string name, string content)
    {
        var filePath = Path.Combine(_path, $"{name}.cq");
        var file = new FileScope(name, filePath, content, _project.ProjectNamespace!);
        _project.ProjectNamespace!.AddScope(file);

        return this;
    }
}
