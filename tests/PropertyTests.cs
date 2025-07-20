using Caique.Tests.Utilities;

namespace Caique.Tests;

public class PropertyTests
{
    [Test]
    public void TestGetterOnly()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let c = new C();

                    c.a + c.b + c.c + c.d + c.e
                }
            }

            class C
            {
                pub let a i32 { 2 }

                pub let b i32
                {
                    get -> 3;
                }

                pub let c i32
                {
                    get { 5 }
                }

                pub let d i32 -> 7;

                pub let e i32
                {
                    get
                    {
                        return d + 1;
                    }
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(25);
    }

    [Test]
    public void TestGetterAndSetter()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let c = new C();
                    let a1 = c.a;
                    c.a = 3;

                    a1 + c.a
                }
            }

            class C
            {
                pub var a i32
                {
                    get -> internal;
                    set -> internal = value * 2;
                }

                var internal i32 = 2;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(8);
    }

    [Test]
    public void TestSetter_WithoutGetter_Error()
    {
        var mainFile = """
            class C
            {
                pub var a i32
                {
                    set -> internal = value * 2;
                }

                var internal i32 = 2;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorSetterButNoGetter);
    }

    [Test]
    public void TestSetter_WithLet_Error()
    {
        var mainFile = """
            class C
            {
                pub let a i32
                {
                    get -> internal;
                    set -> internal = value * 2;
                }

                var internal i32 = 2;
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorSetterOnImmutable);
    }
}
