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
                func Speak() i32
                {
                    42
                }
            }

            inheritable class Animal
            {
                func Speak() i32
                {
                    12
                }
            }
            """;
        var result = TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertNoBuildErrors();

        Assert.That(result.ExitCode, Is.EqualTo(42 + 12));
    }
}
