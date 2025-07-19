using Caique.Scope;
using Caique.Tests.Utilities;

namespace Caique.Tests;

public class BlockTests
{
    [Test]
    public void TestBlock_WithReturnValue()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    {
                        2
                    }
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
    public void TestBlock_WithReturnValueAndVariable()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let x =
                    {
                        2
                    };

                    x
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }
}
