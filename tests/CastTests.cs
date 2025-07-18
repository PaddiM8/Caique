using Caique.Tests.Utilities;

namespace Caique.Tests;

public class CastTests
{
    [Test]
    public void TestCastToInt16()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let a i8 = 2;
                    let b i16 = 3;
                    let c i32 = 5;
                    let d i64 = 7;
                    let e f32 = 11.0;
                    let f f64 = 13.0;

                    (a.as(i16) + b.as(i16) + c.as(i16) + d.as(i16) + e.as(i16) + f.as(i16)).as(i32)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(41);
    }

    [Test]
    public void TestCastToInt32()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let a i8 = 2;
                    let b i16 = 3;
                    let c i32 = 5;
                    let d i64 = 7;
                    let e f32 = 11.0;
                    let f f64 = 13.0;

                    a.as(i32) + b.as(i32) + c.as(i32) + d.as(i32) + e.as(i32) + f.as(i32)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(41);
    }

    [Test]
    public void TestCastToInt64()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let a i8 = 2;
                    let b i16 = 3;
                    let c i32 = 5;
                    let d i64 = 7;
                    let e f32 = 11.0;
                    let f f64 = 13.0;

                    (a.as(i64) + b.as(i64) + c.as(i64) + d.as(i64) + e.as(i64) + f.as(i64)).as(i32)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(41);
    }

    [Test]
    public void TestCastToFloat32()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let a i8 = 2;
                    let b i16 = 3;
                    let c i32 = 5;
                    let d i64 = 7;
                    let e f32 = 11.0;
                    let f f64 = 13.0;

                    (a.as(f32) + b.as(f32) + c.as(f32) + d.as(f32) + e.as(f32) + f.as(f32)).as(i32)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(41);
    }

    [Test]
    public void TestCastToFloat64()
    {
        var mainFile = """
            module Main
            {
                func Run() i32
                {
                    let a i8 = 2;
                    let b i16 = 3;
                    let c i32 = 5;
                    let d i64 = 7;
                    let e f32 = 11.0;
                    let f f64 = 13.0;

                    (a.as(f64) + b.as(f64) + c.as(f64) + d.as(f64) + e.as(f64) + f.as(f64)).as(i32)
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Run()
            .AssertSuccessWithExitCode(41);
    }

    [Test]
    public void TestCastToPrimitiveToStruct_Error()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    2.as(String);
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorInvalidCast);
    }

    [Test]
    public void TestCastStructToPrimitive()
    {
        var mainFile = """
            module Main
            {
                func Run()
                {
                    "hello".as(i32);
                }
            }
            """;
        TestProject
            .Create()
            .AddFile("main", mainFile)
            .Compile()
            .AssertSingleCompilationError(DiagnosticCode.ErrorInvalidCast);
    }
}
