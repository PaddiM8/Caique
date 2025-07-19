using Caique.Scope;
using Caique.Tests.Utilities;

namespace Caique.Tests;

public class ComparisonTests
{
    [Test]
    public void TestEqualityComparison_WithIntegers()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if 2 == 2 && !(2 == 3) && !(2 != 2) && 2 != 3
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
    public void TestGreater_WithIntegers()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if 3 > 2 && !(2 > 2) && !(2 > 3)
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
