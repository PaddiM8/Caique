using Caique.Tests.Utilities;

namespace Caique.Tests;

public class ModuleTests
{
    [Test]
    public void TestModuleWithStaticField_AndNonPrimitiveValue_IsConstructedOnlyOnce()
    {
        var mainFile = """
            module Main
            {
                let a i32 = GetValue();
                var counter i32 = 0;

                func Run() i32
                {
                    a + a + counter
                }

                func GetValue() i32
                {
                    counter = counter + 1;
                    2
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
    public void TestModuleWithStaticField_AndMutatedNonPrimitiveValue()
    {
        var mainFile = """
            module Main
            {
                var a i32 = GetValue();

                func Run() i32
                {
                    let x = a;
                    a = 3;

                    a + x
                }

                func GetValue() i32
                {
                    2
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(5);
    }
}
