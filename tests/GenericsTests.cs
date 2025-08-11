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
                    a.x = 2;

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
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericClass_WithGenericFieldAndGenericInstantiation()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let b = new B[i32](2);

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
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericClass_WithFunction()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    new A[i32]().Func(2)
                }
            }

            class A[T]
            {
                pub func Func(value T) T
                {
                    value
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericFunction_InSameModule()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    Generic[i32](2)
                }

                func Generic[T](value T) T
                {
                    value
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericFunction_InDifferentClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let other = new Other();

                    other.Generic[i32](2)
                }
            }

            class Other
            {
                pub func Generic[T](value T) T
                {
                    value
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericFunction_InGenericClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let other = new Other[i64]();
                    let a = other.Generic[i32]() == size_of(i64) + size_of(i32);
                    let b = other.Generic[i8]() == size_of(i64) + size_of(i8);

                    if a && b
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }

            class Other[T]
            {
                pub func Generic[K]() usize
                {
                    size_of(T) + size_of(K)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericFunction_WithGenericCallInGenericClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    if new B[i64]().Generic[i32]() == size_of(i64) + size_of(i32)
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }

            class A
            {
                pub func Generic[T, K]() usize
                {
                    size_of(T) + size_of(K)
                }
            }

            class B[T]
            {
                pub func Generic[K]() usize
                {
                    new A().Generic[T, K]()
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericFunctionReference()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let ref = Generic[i64, i32];
                    if ref() == size_of(i64) + size_of(i32)
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }

                func Generic[T, K]() usize
                {
                    size_of(T) + size_of(K)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericFunction_InProtocol()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let p P = new A();
                    if p.Generic[i64, i32]() == size_of(i64) + size_of(i32)
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }

            }

            protocol P
            {
                func Generic[T, K]() usize;
            }

            class A : P
            {
                pub func Generic[T, K]() usize
                {
                    size_of(T) + size_of(K)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    // TestGenericFunction_InInheritedClass
    // TestGenericFunction_InInheritedClassWithParent
    // TestNonGenericFunction_InGenericProtocol
    // TestNonGenericFunction_InGenericInheritedClass
}
