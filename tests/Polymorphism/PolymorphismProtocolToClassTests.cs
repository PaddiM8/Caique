using Caique.Tests.Utilities;

namespace Caique.Tests.Polymorphism;

public class PolymorphismProtocolToClassTests
{
    [Test]
    public void TestProtocolToClassByCast()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let speakable Speakable = new Duck();
                    let other = speakable.Speak();

                    let duck = speakable.as(Duck);
                    other + duck.Speak()
                }
            }

            class Duck : Speakable
            {
                func Speak() i32
                {
                    42
                }
            }

            protocol Speakable
            {
                func Speak() i32;
            }
            """;
        var result = TestProject
            .Create(nameof(TestProtocolToClassByCast))
            .AddFile("main", mainFile)
            .Run()
            .AssertNoBuildErrors();

        Assert.That(result.ExitCode, Is.EqualTo(42 + 42));
    }
}
