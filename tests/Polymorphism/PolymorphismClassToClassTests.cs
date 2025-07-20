using Caique.Tests.Utilities;

namespace Caique.Tests.Polymorphism;

public class PolymorphismClassToClassTests
{
    [Test]
    public void TestClassToClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let animal Animal = new Duck();
                    let other = animal.Speak();

                    let duck = animal.as(Duck);
                    other + duck.Speak()
                }
            }

            class Duck : Animal
            {
                pub func Speak() i32
                {
                    42
                }
            }

            inheritable class Animal
            {
                pub func Speak() i32
                {
                    12
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(42 + 12);
    }

    [Test]
    public void TestInheritFromNonInheritableClass_Error()
    {
        var mainFile = """
            class Duck : Animal
            {
            }

            class Animal
            {
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorBaseClassNotInheritable);
    }
}
