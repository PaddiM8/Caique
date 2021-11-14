using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Caique.Ast;
using Caique.CodeGeneration;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Printing;
using Caique.Util;

namespace Caique
{
    /// <summary>
    /// Takes care of the entire compilation process,
    /// such as lexing, parsing, type checking, etc.
    /// All it needs is a module tree (with file paths) and the
    /// path to the source files.
    /// </summary>
    public class Compilation
    {
        public DiagnosticBag Diagnostics { get; private set; } = new DiagnosticBag();

        public bool PrintTokens { get; set; }

        public bool PrintAst { get; set; }

        public bool PrintEnvironment { get; set; }

        public bool PrintLlvm { get; set; }

        private readonly string _rootPath = "";
        private readonly Dictionary<string, string> _libraryPaths;

        public Compilation(string rootPath, Dictionary<string, string> libraryPaths)
        {
            _rootPath = rootPath;
            _libraryPaths = libraryPaths;
        }

        public void Compile(string targetPath)
        {
            string preludePath = Path.Combine(_libraryPaths["core"], "../prelude/src");
            var prelude = new ModuleEnvironment(
                "prelude",
                preludePath,
                targetPath,
                new Dictionary<string, string>(),
                Diagnostics,
                null
            );

            prelude.CreateChildModule(
                "lib",
                Path.Combine(preludePath, "lib.cq")
            );

            var rootModule = new ModuleEnvironment(
                "root",
                _rootPath,
                targetPath,
                _libraryPaths,
                Diagnostics,
                prelude
            );

            // Parsing, type checking, and code generation is done on the fly
            // in ModuleEnvironment
            rootModule.CreateChildModule(
                "main",
                Path.Combine(_rootPath, "main.cq")
            );

            // Code generation
            prelude.GenerateSymbols();
            rootModule.GenerateSymbols();
            prelude.GenerateContent();
            rootModule.GenerateContent();

            var outputType = PrintLlvm
                ? OutputType.IntermediateRepresentation
                : OutputType.ObjectFile;
            prelude.Emit(outputType);
            rootModule.Emit(outputType);

            if (PrintTokens) ObjectPrinter.PrintTokens(rootModule);
            if (PrintAst) ObjectPrinter.PrintAst(rootModule);
            if (PrintEnvironment) ObjectPrinter.PrintSymbols(rootModule);

            foreach (var diagnostic in Diagnostics)
                diagnostic.Print();
        }
    }
}