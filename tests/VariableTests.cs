using Caique.Tests.Utilities;

namespace Caique.Tests;

public class VariableTests
{
    [Test]
    public void TestVariable()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let x = 3;
                    x
                }
            }
            """;
        TestProject
            .Create(nameof(TestVariable))
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }

    [Test]
    public void TestVariableReferenceBeforeDeclaration_Error()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    x;
                    let x = 3;
                }
            }
            """;
        TestProject
            .Create(nameof(TestVariableReferenceBeforeDeclaration_Error))
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorNotFound);
    }
}
