using Caique.Tests.Utilities;

namespace Caique.Tests;

public class CommentTests
{
    [Test]
    public void TestSingleLineComment()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    // Some comment ++
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSuccess();
    }

    [Test]
    public void TestMultiLineComment()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    /* Some comment ++
                       spanning multiple lines
                   */
                }
            }
            /*
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSuccess();
    }
}
