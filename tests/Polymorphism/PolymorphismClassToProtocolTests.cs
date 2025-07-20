using Caique.Tests.Utilities;

namespace Caique.Tests.Polymorphism;

public class PolymorphismClassToProtocolTests
{
    [Test]
    public void TestClassToProtocolByVariable()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let speakable Speakable = new Duck();
                    speakable.Speak()
                }
            }

            class Duck : Speakable
            {
                pub func Speak() i32
                {
                    42
                }
            }

            protocol Speakable
            {
                func Speak() i32;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(42);
    }

    [Test]
    public void TestClassToProtocolByParameter()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let duck = new Duck();
                    Quack(duck)
                }

                func Quack(speakable Speakable) i32
                {
                    speakable.Speak()
                }
            }

            class Duck : Speakable
            {
                pub func Speak() i32
                {
                    42
                }
            }

            protocol Speakable
            {
                func Speak() i32;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(42);
    }

    [Test]
    public void TestClassToProtocolByCast()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let duck = new Duck();
                    let speakable = duck.as(Speakable);
                    speakable.Speak()
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
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(42);
    }

    [Test]
    public void TestClassToProtocolByVariableThenPassAsArgument()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let speakable Speakable = new Duck();
                    Speak(speakable)
                }

                func Speak(speakable Speakable) i32
                {
                    speakable.Speak()
                }
            }

            class Duck : Speakable
            {
                pub func Speak() i32
                {
                    42
                }
            }

            protocol Speakable
            {
                func Speak() i32;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(42);
    }
}
