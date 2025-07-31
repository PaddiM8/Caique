using Caique.Tests.Utilities;

namespace Caique.Tests;

public class GenericsTests
{
    [Test]
    public void TestGenericClass_WithGenericFieldAndSingleInstantiation()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let a = new A[i32]();
                    a.x = 3;

                    a.x
                }
            }

            class A[T]
            {
                pub let x T;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }

    [Test]
    public void TestGenericClass_WithGenericFieldAndGenericInstantiation()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let b = new B[i32](3);

                    b.a.x
                }
            }

            class A[K]
            {
                pub var x K;
            }

            class B[T]
            {
                pub var a A[T];

                init(x T)
                {
                    a = new A[T]();
                    a.x = x;
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }

    // TODO: someInstance.SomeMethod(x) where someInstance is SomeClass[T] and SomeMethod is SomeMethod(x T)
    // TODO: Return a generic type to a different class
    // TODO: A[T, K] where T is from a class and K is from a function
}
