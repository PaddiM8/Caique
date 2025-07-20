using Caique.Tests.Utilities;

namespace Caique.Tests;

public class ImportTests
{
    [Test]
    public void TestImportModuleFunction()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    Sub:A();
                }
            }
            """;
        var subFile = """
            module Sub
            {
                pub func A()
                {
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddFile("sub", subFile)
            .Run()
            .AssertSuccess();
    }

    [Test]
    public void TestImportClass()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    new Sub().A();
                }
            }
            """;
        var subFile = """
            class Sub
            {
                pub func A()
                {
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddFile("sub", subFile)
            .Run()
            .AssertSuccess();
    }

    [Test]
    public void TestImportNamespace()
    {
        var mainFile = """
            with nested;

            module Main
            {
                func Run()
                {
                    new Sub().A();
                }
            }
            """;
        var subFile = """
            class Sub
            {
                pub func A()
                {
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddNamespace("nested", builder =>
                builder.AddFile("sub", subFile)
            )
            .Run()
            .AssertSuccess();
    }
}
