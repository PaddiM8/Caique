using Caique.Tests.Utilities;

namespace Caique.Tests;

public class EnumTests
{
    [Test]
    public void TestDefaultEnumWithCast()
    {
        var mainFile = """
            enum A
            {
                A,
                B,
                C,
            }

            module Main
            {
                func Run() i32
                {
                    A:A.as(i32) + A:B.as(i32) + A:C.as(i32)
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
    public void TestEnumWithAssignedValueForOneMemberAndCast()
    {
        var mainFile = """
            enum A
            {
                A,
                B = 5,
                C,
            }

            module Main
            {
                func Run() i32
                {
                    A:A.as(i32) + A:B.as(i32) + A:C.as(i32)
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
    public void TestEnumComparison()
    {
        var mainFile = """
            enum A
            {
                A,
                B = 5,
                C,
            }

            module Main
            {
                func Run() i32
                {
                    if A:B == A:B
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
}
