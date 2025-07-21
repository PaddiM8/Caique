using Caique.Tests.Utilities;

namespace Caique.Tests.Std;

public class IoTests
{
    [Test]
    public void TestIoPrintln()
    {
        var mainFile = """
            with std;

            module Main
            {
                func Run()
                {
                    Io:Println("Hello World");
                }
            }
            """;
        var result = TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccess();
        Assert.That(result.Stdout, Is.EqualTo("Hello World" + Environment.NewLine));
    }
}
