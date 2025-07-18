using Caique.Scope;
using Caique.Tests.Utilities;

namespace Caique.Tests;

public class PlainTests
{
    [Test]
    public void TestEmptyRunFunction()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                }
            }
            """;
        TestProject
            .Create(nameof(TestEmptyRunFunction))
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccess();
    }
}
