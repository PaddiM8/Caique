using Caique.Tests.Utilities;

namespace Caique.Tests;

public class AndOrTests
{
    [Test]
    public void TestAnd_WithBothTrue()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true && true
                    {
                        2
                    }
                    else
                    {
                        3
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
    public void TestAnd_WithLeftFalse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if false && true
                    {
                        2
                    }
                    else
                    {
                        3
                    }
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
    public void TestAnd_WithRightFalse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true && false
                    {
                        2
                    }
                    else
                    {
                        3
                    }
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
    public void TestAnd_WithBothFalse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if false && false
                    {
                        2
                    }
                    else
                    {
                        3
                    }
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
    public void TestOr_WithBothTrue()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true || true
                    {
                        2
                    }
                    else
                    {
                        3
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
    public void TestOr_WithLeftFalse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if false || true
                    {
                        2
                    }
                    else
                    {
                        3
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
    public void TestOr_WithRightFalse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true || false
                    {
                        2
                    }
                    else
                    {
                        3
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
    public void TestOr_WithBothFalse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if false || false
                    {
                        2
                    }
                    else
                    {
                        3
                    }
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
