using System.Diagnostics;
using Caique.Analysis;
using Caique.Backend;
using Caique.Linking;
using Caique.Parsing;
using Caique.Preprocessing;
using Caique.Resolving;
using Caique.Scope;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique;

public class Compilation
{
    public static IEnumerable<Diagnostic> Compile(string projectFilePath, CompilationOptions? options = null)
    {
        options ??= new CompilationOptions();

        var preludeProject = Preprocessor.Process("prelude", "/home/paddi/projects/caique/prelude", prelude: null);
        var stdProject = Preprocessor.Process("std", "/home/paddi/projects/caique/std", preludeProject.ProjectNamespace);

        // TODO: The actual project name should be defined in the project file
        var project = Preprocessor.Process("root", projectFilePath, preludeProject.ProjectNamespace);
        var context = new CompilationContext(preludeProject.ProjectNamespace!);
        project.AddDependency(preludeProject);
        project.AddDependency(stdProject);

        Parse(project, context);

        if (!context.DiagnosticReporter.Errors.Any())
        {
            Resolve(project, context);
        }

        List<(SemanticTree, FileScope)> semanticTrees = [];
        if (!context.DiagnosticReporter.Errors.Any())
        {
            semanticTrees = Analyse(project, context);
        }

        if (!context.DiagnosticReporter.Errors.Any())
        {
            var objectFilePaths = Emit(semanticTrees, projectFilePath, context, options);
            Link(objectFilePaths, project, GetTargetDirectory(projectFilePath));
        }

        return context.DiagnosticReporter.Diagnostics;
    }

    private static void Parse(Project project, CompilationContext context)
    {
        foreach (var dependency in project.Dependencies.Values)
        {
            dependency.ProjectNamespace!.Traverse(scope =>
            {
                var content = File.ReadAllText(scope.FilePath);
                scope.SyntaxTree = Parser.Parse(content, scope, context);
            });
        }

        project.ProjectNamespace!.Traverse(scope =>
        {
            var content = File.ReadAllText(scope.FilePath);
            scope.SyntaxTree = Parser.Parse(content, scope, context);
        });
    }

    private static void Resolve(Project project, CompilationContext context)
    {
        foreach (var dependency in project.Dependencies.Values)
        {
            dependency.ProjectNamespace!.Traverse(scope =>
            {
                Debug.Assert(scope.SyntaxTree != null);
                Resolver.Resolve(scope.SyntaxTree, context);
            });
        }

        project.ProjectNamespace!.Traverse(scope =>
        {
            Debug.Assert(scope.SyntaxTree != null);
            Resolver.Resolve(scope.SyntaxTree, context);
        });
    }

    private static List<(SemanticTree, FileScope)> Analyse(Project project, CompilationContext context)
    {
        var semanticTrees = new List<(SemanticTree, FileScope)>();
        foreach (var dependency in project.Dependencies.Values)
        {
            dependency.ProjectNamespace!.Traverse(scope =>
            {
                Debug.Assert(scope.SyntaxTree != null);
                var semanticTree = Analyser.Analyse(scope.SyntaxTree, context);
                semanticTrees.Add((semanticTree, scope));
            });
        }

        project.ProjectNamespace!.Traverse(scope =>
        {
            Debug.Assert(scope.SyntaxTree != null);
            var semanticTree = Analyser.Analyse(scope.SyntaxTree, context);
            semanticTrees.Add((semanticTree, scope));
        });

        return semanticTrees;
    }

    private static List<string> Emit(
        List<(SemanticTree, FileScope)> semanticTrees,
        string projectFilePath,
        CompilationContext compilationContext,
        CompilationOptions options
    )
    {
        using var llvmContext = LLVMContextRef.Create();
        var emitterContextMap = new Dictionary<SemanticTree, LlvmEmitterContext>();
        var llvmContextCache = new LlvmContextCache();
        foreach (var (semanticTree, scope) in semanticTrees)
        {
            var moduleName = scope.Namespace.ToString() + "_" + Path.GetFileNameWithoutExtension(scope.FilePath);
            var emitterContext = new LlvmEmitterContext(moduleName, llvmContext, llvmContextCache);
            LlvmHeaderEmitter.Emit(semanticTree, emitterContext);

            emitterContextMap[semanticTree] = emitterContext;
        }

        var objectFilePaths = new List<string>();
        foreach (var (semanticTree, scope) in semanticTrees)
        {
            var emitterContext = emitterContextMap[semanticTree];
            var targetPath = GetTargetDirectory(projectFilePath);
            string objectFilePath = LlvmContentEmitter.Emit(semanticTree, emitterContext, targetPath, compilationContext, options);
            objectFilePaths.Add(objectFilePath);
            emitterContext.Dispose();
        }

        return objectFilePaths;
    }

    private static void Link(IEnumerable<string> objectFilePaths, Project project, string targetPath)
    {
        ILinker linker;
        if (OperatingSystem.IsWindows())
        {
            // TODO: Support link.exe
            throw new NotImplementedException();
        }
        else
        {
            linker = new ClangLinker();
        }

        string outputPath = Path.Combine(targetPath, project.Name);
        linker.Link(objectFilePaths, outputPath);
    }

    private static string GetTargetDirectory(string rootDirectoryPath)
    {
        var path = Path.Combine(rootDirectoryPath, "target");
        Directory.CreateDirectory(path);

        return path;
    }
}
