using Caique.Tests.Utilities;

namespace Caique.Tests;

public class FunctionTests
{
    [Test]
    public void TestFunctionCallSameModule()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    A()
                }

                func A() i32
                {
                    3
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
    public void TestFunctionCallDifferentModule()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    Other:A()
                }
            }

            module Other
            {
                func A() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }
}
