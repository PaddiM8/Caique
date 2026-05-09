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
                    let a = p.Generic[i64, i32]() == size_of(i64) + size_of(i32);
                    let b = p.Generic[i32, i32]() == size_of(i32) + size_of(i32);
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

    [Test]
    public void TestGenericFunction_InInheritedClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let p B = new A();
                    let a = p.Generic[i64, i32]() == size_of(i64) + size_of(i32);
                    let b = p.Generic[i32, i32]() == size_of(i32) + size_of(i32);
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

            inheritable class B
            {
                pub func Generic[T, K]() usize
                {
                    size_of(T) + size_of(K)
                }
            }

            class A : B
            {
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericFunction_InInheritedParentClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let p C = new A();
                    let a = p.Generic[i64, i32]() == size_of(i64) + size_of(i32);
                    let b = p.Generic[i32, i32]() == size_of(i32) + size_of(i32);
                    let c = new B().Generic[i32, i32]() == size_of(i32) + size_of(i32) + 1;
                    if a && b && c
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }

            inheritable class C
            {
                pub func Generic[T, K]() usize
                {
                    size_of(T) + size_of(K) + 1
                }

                pub func NonGeneric3() i32
                {
                    2
                }
            }

            inheritable class B : C
            {
                pub func NonGeneric() i32
                {
                    1
                }
            }

            class A : B
            {
                pub override func NonGeneric() i32
                {
                    4
                }

                pub override func Generic[T, K]() usize
                {
                    size_of(T) + size_of(K)
                }

                pub func NonGeneric2()
                {
                    3
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
    public void TestMultipleGenericFunctions_InInheritedParentClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let p C = new A();
                    let a = p.Generic1[i64, i32]() == size_of(i64) + size_of(i32) + 4;
                    let b = p.Generic2[i64, i64]() == size_of(i64) + size_of(i64) + 5;
                    let c = p.Generic3[i64, i32]() == size_of(i64) + size_of(i32) + 6;
                    let d = new B().Generic2[i32, i32]() == size_of(i32) + size_of(i32) + 2;
                    if a && b && c && d
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }

            inheritable class C
            {
                pub func Generic1[T, K]() usize
                {
                    size_of(T) + size_of(K) + 1
                }

                pub func Generic2[T, K]() usize
                {
                    size_of(T) + size_of(K) + 2
                }

                pub func Generic3[T, K]() usize
                {
                    size_of(T) + size_of(K) + 3
                }
            }

            inheritable class B : C
            {
            }

            class A : B
            {
                pub override func Generic1[T, K]() usize
                {
                    size_of(T) + size_of(K) + 4
                }

                pub override func Generic2[T, K]() usize
                {
                    size_of(T) + size_of(K) + 5
                }

                pub override func Generic3[T, K]() usize
                {
                    size_of(T) + size_of(K) + 6
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
    public void TestGenericFunction_InInheritedGenericClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let p1 B[i32] = new A[i32]();
                    let p2 B[i64] = new A[i64]();
                    let a = p1.Generic[i64]() == size_of(i32) + size_of(i64);
                    let b = p1.Generic[i32]() == size_of(i32) + size_of(i32);
                    let c = p2.Generic[i64]() == size_of(i64) + size_of(i64);
                    let d = p2.Generic[i32]() == size_of(i64) + size_of(i32);
                    if a && b && c && d
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }

            }

            inheritable class B[T]
            {
                pub func Generic[K]() usize
                {
                    size_of(T) + size_of(K)
                }
            }

            class A[T] : B[T]
            {
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestNonGenericFunction_InInheritedGenericClass()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let p1 B[i32] = new A[i32]();
                    let p2 B[i64] = new A[i64]();
                    let a = p1.NonGeneric() == size_of(i32);
                    let b = p2.NonGeneric() == size_of(i64);
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

            inheritable class B[T]
            {
                pub func NonGeneric() usize
                {
                    size_of(T)
                }
            }

            class A[T] : B[T]
            {
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericClass_WithNestedSelf()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let p A[A[i32]] = new A[A[i32]](new A[i32](3));
                    if p.x.x == 3
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }

            }

            class A[T]
            {
                pub var x T;

                init(x)
                {
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
    public void TestGenericVirtualFunction_InDifferentNamespace()
    {
        var mainFile = """
            with other;

            module Main
            {
                func Run() i32
                {
                    let p P = new A();
                    let a = p.Generic[i32]() == size_of(i32);
                    let b = p.Generic[i64]() == size_of(i64);
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
            """;
        var otherFile = """
            protocol P
            {
                func Generic[T]() usize;
            }

            class A : P
            {
                pub func Generic[T]() usize
                {
                    size_of(T)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddNamespace(
                "other",
                other => other.AddFile("otherFile", otherFile)
            )
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericVirtualFunctions_InDifferentGenericClasses_InDifferentNamespaces()
    {
        var mainFile = """
            with second_namespace;
            with third_namespace;

            module Main
            {
                func Run() i32
                {
                    let p1 P = new A[i32]();
                    let p2 P = new A[i64]();
                    let p3 P = new B[i32]();
                    let a1 = p1.Generic[i32]() == size_of(i32) + size_of(i32);
                    let a2 = p2.Generic[i32]() == size_of(i32) + size_of(i64);

                    let b1 = p1.Generic[i64]() == size_of(i64) + size_of(i32);
                    let b2 = p2.Generic[i64]() == size_of(i64) + size_of(i64);

                    let c = p3.Generic[i32]() == size_of(i32) + size_of(i32);
                    let d = p3.Generic[i64]() == size_of(i64) + size_of(i32);

                    if a1 && a2 && b1 && b2 && c && d
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }
            """;
        var secondFile = """
            with test:third_namespace;

            protocol P
            {
                func Generic[T]() usize;
            }

            class A[T] : P
            {
                pub func Generic[K]() usize
                {
                    new B[T]().Generic[K]()
                }
            }
            """;
        var thirdFile = """
            with test:second_namespace;

            class B[T] : P
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
            .AddNamespace(
                "second_namespace",
                other => other.AddFile("second_file", secondFile)
            )
            .AddNamespace(
                "third_namespace",
                other => other.AddFile("third_file", thirdFile)
            )
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericVirtualFunctions_InClassThatInheritsClassAndImplementsProtocol()
    {
        var mainFile = """
            with second_namespace;

            module Main
            {
                func Run() i32
                {
                    let p P = new B();
                    let a A = new A();
                    let b B = new B();

                    let r1 = p.Generic1[i32]() == size_of(i32);
                    let r2 = a.Generic2[i64]() == size_of(i64);
                    let r3 = b.Generic1[i16]() == size_of(i16);
                    let r4 = b.Generic2[i16]() == size_of(i16);

                    if r1 && r2 && r3 && r4
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }
            """;
        var secondFile = """
            protocol P
            {
                func Generic1[T]() usize;
            }

            inheritable class A
            {
                pub func Generic2[T]() usize
                {
                    size_of(T)
                }
            }

            class B : A, P
            {
                pub func Generic1[T]() usize
                {
                    size_of(T)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddNamespace(
                "second_namespace",
                other => other.AddFile("second_file", secondFile)
            )
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericVirtualFunctions_InClassThatInheritsClassThatImplementsProtocol()
    {
        var mainFile = """
            with second_namespace;

            module Main
            {
                func Run() i32
                {
                    let p P = new B();
                    let a A = new A();
                    let b B = new B();

                    let r1 = p.Generic2[i32]() == size_of(i32) + 1;
                    let r2 = a.Generic2[i64]() == size_of(i64);
                    let r3 = b.Generic1[i16]() == size_of(i16);
                    let r4 = b.Generic2[i16]() == size_of(i16) + 1;

                    if r1 && r2 && r3 && r4
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }
            """;
        var secondFile = """
            protocol P
            {
                func Generic2[T]() usize;
            }

            inheritable class A : P
            {
                pub func Generic2[T]() usize
                {
                    size_of(T)
                }
            }

            class B : A
            {
                pub func Generic1[T]() usize
                {
                    size_of(T)
                }

                pub override func Generic2[T]() usize
                {
                    size_of(T) + 1
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddNamespace(
                "second_namespace",
                other => other.AddFile("second_file", secondFile)
            )
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    [Test]
    public void TestGenericVirtualFunctions_InGenericClassThatInheritsGenericClassThatImplementsProtocol()
    {
        var mainFile = """
            with second_namespace;

            module Main
            {
                func Run() i32
                {
                    let p P = new B[i32, i64]();
                    let a A[i32] = new A[i32]();
                    let b B[i32, i64] = new B[i32, i64]();

                    let r1 = p.Generic2[i32]() == size_of(i32) + size_of(i32) + 1;
                    let r2 = a.Generic2[i64]() == size_of(i64) + size_of(i32);
                    let r3 = b.Generic2[i16]() == size_of(i16) + size_of(i32) + 1;

                    if r1 && r2 && r3
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }
            """;
        var secondFile = """
            protocol P
            {
                func Generic2[T]() usize;
            }

            inheritable class A[Y] : P
            {
                pub func Generic2[T]() usize
                {
                    size_of(T) + size_of(Y)
                }
            }

            class B[X, Y] : A[Y]
            {
                pub override func Generic2[T]() usize
                {
                    size_of(T) + size_of(X) + 1
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddNamespace(
                "second_namespace",
                other => other.AddFile("second_file", secondFile)
            )
            .Run()
            .AssertSuccessWithExitCode(2);

    }

    [Test]
    public void TestGenericVirtualFunctions_InGenericClassThatInheritsGenericClassThatImplementsGenericProtocol()
    {
        var mainFile = """
            with second_namespace;

            module Main
            {
                func Run() i32
                {
                    let p P[i32] = new B[i32, i64]();
                    let a A[i32] = new A[i32]();
                    let b B[i32, i64] = new B[i32, i64]();

                    let r1 = p.Generic2[i32]() == size_of(i32) + size_of(i32) + 1;
                    let r2 = a.Generic2[i64]() == size_of(i64) + size_of(i32);
                    let r3 = b.Generic2[i16]() == size_of(i16) + size_of(i32) + 1;

                    if r1 && r2 && r3
                    {
                        2
                    }
                    else
                    {
                        3
                    }
                }
            }
            """;
        var secondFile = """
            protocol P[Y]
            {
                func Generic2[T]() usize;
            }

            inheritable class A[Y] : P[Y]
            {
                pub func Generic2[T]() usize
                {
                    size_of(T) + size_of(Y)
                }
            }

            class B[X, Y] : A[Y]
            {
                pub override func Generic2[T]() usize
                {
                    size_of(T) + size_of(X) + 1
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .AddNamespace(
                "second_namespace",
                other => other.AddFile("second_file", secondFile)
            )
            .Run()
            .AssertSuccessWithExitCode(2);
    }

    // TODO: TestMultipleGenericFunctions_InInheritedParentClass but with more layers of inheritance with empty classes in-between
    // TODO: TestMultipleGenericFunctions_InInheritedParentClass with `class B : C, P` where P is a protocol with the generic method
    // TODO: Don't allow type parameter shadowing
    // TODO: Another test class for testing the semantic checks (testing the error handling)
    // TODO: Recursive ones or whatever
    // TODO: Type checking for generic protocols
    // TODO: Document describing how generics work
}
