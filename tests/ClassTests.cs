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
                pub let a i32;
                pub let b i32;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(3);
    }

    [Test]
    public void TestClassWithFieldAccess_ToPrivate_Error()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    let c = new C();
                    c.a = 3;
                }
            }

            class C
            {
                var a i32;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorSymbolIsPrivate);
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
                let a i32;
                var b i32;

                init(a, x i32)
                {
                    b = x;
                }

                pub func F() i32
                {
                    a + b
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
    public void TestClassFieldAssignment_WithImmutable_Error()
    {
        var mainFile = """
            class C
            {
                let a i32;

                init()
                {
                    a = 3;
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorAssignmentToImmutable);
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
                pub func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorNonStaticSymbolReferencedAsStatic);
    }

    public void TestClassWithMethod_AsPrivate_Error()
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
                func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorSymbolIsPrivate);
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
                pub static func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create()
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
                pub static func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create()
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
                pub static func F() i32
                {
                    3
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorExpectedValueGotType);
    }

    [Test]
    public void TestClassWithStaticFunction_AsPrivate_Error()
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
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorSymbolIsPrivate);
    }
}
