using Caique.Tests.Utilities;

namespace Caique.Tests;

public class IfTests
{
    [Test]
    public void TestNonValueIf_WithReturn()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true
                    {
                        return 2;
                    }
                    else
                    {
                        return 3;
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
    public void TestNonValueElse_WithReturn()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if false
                    {
                        return 2;
                    }
                    else
                    {
                        return 3;
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
    public void TestNonValueElseIf_WithReturn()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if false
                    {
                        return 2;
                    }
                    else if true
                    {
                        return 3;
                    }

                    return 0;
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
    public void TestNonValueIf_WithoutReturn()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let result = 0;
                    if true
                    {
                        result = 2;
                    }
                    else
                    {
                        result = 3;
                    }

                    result
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
    public void TestNonValueElse_WithoutReturn()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let result = 0;
                    if false
                    {
                        result = 2;
                    }
                    else
                    {
                        result = 3;
                    }

                    result
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
    public void TestValueIf_WithImplicitReturn()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true
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
    public void TestValueIf_WithVariable()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let result = if true
                    {
                        2
                    }
                    else
                    {
                        3
                    };

                    result
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
    public void TestValueIf_WithDifferentReturnTypes_Error()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true
                    {
                        2
                    }
                    else
                    {
                        "hello"
                    }
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorIncompatibleType);
    }

    [Test]
    public void TestValueIf_WithoutReturnTypeForElse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true
                    {
                        2
                    }
                    else
                    {
                        3;
                    }
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorIncompatibleType);
    }

    [Test]
    public void TestEmptyNonValueIf()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true
                    {
                    }

                    2
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
    public void TestValueIf_WithEmptyElse()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true
                    {
                        2
                    }
                    else
                    {
                    }
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorIncompatibleType);
    }

    [Test]
    public void TestNonValueIf_WithDo()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if true
                        do 2
                        else 3
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
