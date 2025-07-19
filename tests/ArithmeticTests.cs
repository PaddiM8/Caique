using Caique.Scope;
using Caique.Tests.Utilities;

namespace Caique.Tests;

public class ArithmeticTests
{
    [Test]
    public void TestUnaryMinus()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    4 + (-2)
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
    public void TestAddition_WithIntegers()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    2 + 3 + 6
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(11);
    }

    [Test]
    public void TestSubtraction_WithIntegers()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    12 - 6 - 3
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
    public void TestMultiplication_WithIntegers()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    2 * 3 * 5
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(30);
    }

    [Test]
    public void TestDivision_WithIntegers()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    22 / 2 / 2
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(5);
    }

    [Test]
    public void TestArithmetic_WithIntegers()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    2 * (3 + 5) / 2
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(8);
    }

    [Test]
    public void TestArithmetic_WithFloats()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    (2.5 * (3.0 + 5.0) / 3 * 10).as(i32)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(66);
    }
}
