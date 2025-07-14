using System.Diagnostics;
using Caique.Analysis;
using Caique.Backend;
using Caique.Linking;
using Caique.Lowering;
using Caique.Parsing;
using Caique.Preprocessing;
using Caique.Resolving;
using Caique.Scope;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique;

public class Compilation
{
    public static IReadOnlyList<Diagnostic> Compile(string projectFilePath, CompilationOptions? options = null)
    {
        var project = Preprocessor.Process("root", projectFilePath);

        return Compile(project, options);
    }

    public static IReadOnlyList<Diagnostic> Compile(Project project, CompilationOptions? options = null)
    {
        options ??= new CompilationOptions();

        var stdProject = Preprocessor.Process("std", "/home/paddi/projects/caique/std");

        // TODO: The actual project name should be defined in the project file
        var context = new CompilationContext(stdProject.ProjectNamespace!);
        project.AddDependency(stdProject);

        Parse(project, context);

        if (!context.DiagnosticReporter.Errors.Any())
            Resolve(project, context);

        List<SemanticTree> semanticTrees = [];
        if (!context.DiagnosticReporter.Errors.Any())
            semanticTrees = Analyse(project, context);

        if (!context.DiagnosticReporter.Errors.Any())
        {
            var loweredTrees = Lower(semanticTrees, context);
            var objectFilePaths = Emit(loweredTrees, project.ProjectFilePath, options);
            Link(objectFilePaths, project, GetTargetDirectory(project.ProjectFilePath));
        }

        return context.DiagnosticReporter.Diagnostics;
    }

    private static void Parse(Project project, CompilationContext context)
    {
        foreach (var dependency in project.Dependencies.Values)
        {
            dependency.ProjectNamespace!.Traverse(scope =>
            {
                scope.SyntaxTree = Parser.Parse(scope, context);
            });
        }

        project.ProjectNamespace!.Traverse(scope =>
        {
            scope.SyntaxTree = Parser.Parse(scope, context);
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

    private static List<SemanticTree> Analyse(Project project, CompilationContext context)
    {
        var semanticTrees = new List<SemanticTree>();
        foreach (var dependency in project.Dependencies.Values)
        {
            dependency.ProjectNamespace!.Traverse(scope =>
            {
                Debug.Assert(scope.SyntaxTree != null);
                var semanticTree = Analyser.Analyse(scope.SyntaxTree, context);
                semanticTrees.Add(semanticTree);
            });
        }

        project.ProjectNamespace!.Traverse(scope =>
        {
            Debug.Assert(scope.SyntaxTree != null);
            var semanticTree = Analyser.Analyse(scope.SyntaxTree, context);
            semanticTrees.Add(semanticTree);
        });

        return semanticTrees;
    }

    private static List<LoweredTree> Lower(List<SemanticTree> semanticTrees, CompilationContext compilationContext
    )
    {
        var loweredTrees = new List<LoweredTree>();
        foreach (var semanticTree in semanticTrees)
        {
            var tree = Lowerer.Lower(semanticTree, compilationContext.StdScope);
            loweredTrees.Add(tree);
        }

        return loweredTrees;
    }

    private static List<string> Emit(
        List<LoweredTree> loweredTrees,
        string projectFilePath,
        CompilationOptions options
    )
    {
        using var llvmContext = LLVMContextRef.Create();
        var objectFilePaths = new List<string>();
        foreach (var loweredTree in loweredTrees)
        {
            var emitterContext = new LlvmEmitterContext(loweredTree.ModuleName, llvmContext);
            var targetPath = GetTargetDirectory(projectFilePath);
            var objectFilePath = LlvmContentEmitter.Emit(
                loweredTree,
                emitterContext,
                targetPath,
                options
            );

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
