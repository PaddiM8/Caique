using Caique.Tests.Utilities;

namespace Caique.Tests.Std;

public class EquatableTests
{
    [Test]
    public void TestEqualityComparison_WithEquatable()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let a = new Box(2);
                    let b = new Box(2);
                    let c = new Box(3);

                    if a == b && a != c
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }

            class Box : Equatable
            {
                let value i32;

                init (value)
                {
                }

                pub func IsEqual(other Box) bool
                {
                    value == other.value
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestEqualityComparison_WithNonEquatable_Error()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    new Box(2) == new Box(2);
                }
            }

            class Box
            {
                let value i32;

                init (value)
                {
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorIncompatibleType);
    }
}
