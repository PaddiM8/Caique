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
            .Create()
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
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorNotFound);
    }

    [Test]
    public void TestVariableAssignment()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    var x = 2;
                    x = 3;

                    x
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }

    [Test]
    public void TestVariableAssignment_WithImmutable_Error()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    let x = 2;
                    x = 3;
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorAssignmentToImmutable);
    }
}
