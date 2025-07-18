using Caique.Tests.Utilities;

namespace Caique.Tests;

public class ClassTests
{
    [Test]
    public void TestClassWithFieldAccess()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let c = new C();
                    c.b = 3;

                    c.a + c.b
                }
            }

            class C
            {
                a i32;
                b i32;
            }
            """;
        TestProject
            .Create(nameof(TestClassWithFieldAccess))
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }

    [Test]
    public void TestClassWithFieldsAndMethod()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let c = new C(2, 3);
                    c.F()
                }
            }

            class C
            {
                a i32;
                b i32;

                init(a, x i32)
                {
                    b = x;
                }

                func F() i32
                {
                    a + b
                }
            }
            """;
        TestProject
            .Create(nameof(TestClassWithFieldsAndMethod))
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(5);
    }

    public void TestClassWithMethod_CallAsStatic_Error()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    C:F()
                }
            }

            class C
            {
                func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create(nameof(TestClassWithMethod_CallAsStatic_Error))
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorNonStaticSymbolReferencedAsStatic);
    }

    [Test]
    public void TestClassWithStaticFunction_CallAsStaticFunction()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    C:F()
                }
            }

            class C
            {
                static func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create(nameof(TestClassWithStaticFunction_CallAsStaticFunction))
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }

    [Test]
    public void TestClassWithStaticFunction_CallAsMethod_Error()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    new C().F()
                }
            }

            class C
            {
                static func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create(nameof(TestClassWithStaticFunction_CallAsMethod_Error))
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorStaticSymbolReferencedAsNonStatic);
    }

    [Test]
    public void TestClassWithStaticFunction_CallWithDotNotation_Error()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    C.F()
                }
            }

            class C
            {
                static func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create(nameof(TestClassWithStaticFunction_CallWithDotNotation_Error))
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorExpectedValueGotType);
    }
}
