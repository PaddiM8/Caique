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

        public ModuleEnvironment Environment { get; private set; }

        public bool PrintTokens { get; set; }

        public bool PrintAst { get; set; }

        public bool PrintEnvironment { get; set; }

        private readonly string _rootPath = "";

        public Compilation(ModuleEnvironment environment, string rootPath)
        {
            Environment = environment;
            _rootPath = rootPath;
        }

        public void Compile()
        {
            // Parse everything first, so that classes and functions
            // are added to the symbol table before type checking.
            // The ParseModuleEnvironment traverses the ModuleEnvironment
            // tree and reads the file specified in the specific ModuleEnvironment.
            var asts = ParseModuleEnvironment(Environment);

            // Only type check if the parsing didn't generate any errors
            if (!Diagnostics.Any())
            {
                foreach (var ast in asts)
                {
                    // Set up diagnostic bag
                    Diagnostics.CurrentFile = RelativePath(
                        ast.ModuleEnvironment.FilePath!
                    );

                    new TypeChecker(
                        ast,
                        Diagnostics
                    ).Analyse();

                    if (!Diagnostics.Any())
                        new LllvmGenerator(ast).Generate();

                    if (PrintAst) ast.Print();
                }
            }

            if (PrintEnvironment) Environment.Print();

            foreach (var diagnostic in Diagnostics)
                diagnostic.Print();
        }

        /// <summary>
        /// Traverses a module tree and reads the files associated
        /// with the different modules, and then parses the contents.
        /// </summary>
        /// <param name="environment">Module tree.</param>
        /// <returns>List of abstract syntax trees.</returns>
        private List<AbstractSyntaxTree> ParseModuleEnvironment(ModuleEnvironment environment)
        {
            return ParseModuleEnvironment(environment, new List<AbstractSyntaxTree>());
        }

        /// <summary>
        /// Traverses a module tree and reads the files associated
        /// with the different modules, and then parses the contents.
        /// </summary>
        /// <param name="environment">Module tree.</param>
        /// <returns>List of abstract syntax trees.</returns>
        private List<AbstractSyntaxTree> ParseModuleEnvironment(ModuleEnvironment environment,
                                                 List<AbstractSyntaxTree> asts)
        {
            foreach (var (_, module) in environment.Modules)
            {
                // If the module is a directory, just parse its children and continue
                if (module.FilePath == null)
                {
                    ParseModuleEnvironment(module, asts);
                    continue;
                }

                // Set up diagnostic bag
                Diagnostics.CurrentFile = RelativePath(module.FilePath);

                // Lex
                var tokens = new Lexer(
                    File.ReadAllText(module.FilePath),
                    Diagnostics)
                .Lex();

                if (PrintTokens) PrintTokenList(tokens);

                // Parse
                var ast = new Parser(
                    tokens,
                    Diagnostics,
                    module
                ).Parse();
                asts.Add(new AbstractSyntaxTree(ast, module));

                ParseModuleEnvironment(module, asts);
            }

            return asts;
        }

        /// <summary>
        /// Turn an absolute path of a file inside 
        /// a Caique project, into a relative path.
        /// </summary>
        /// <param name="path">Absolute path</param>
        /// <returns>Relative path starting at the root of the source folder.</returns>
        private string RelativePath(string path)
        {
            return Path.GetRelativePath(_rootPath, path);
        }

        private static void PrintTokenList(List<Token> tokens)
        {
            foreach (var token in tokens)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(token.Kind);
                Console.ResetColor();

                if (!string.IsNullOrEmpty(token.Value))
                {
                    Console.Write($": {token.Value}");
                }

                Console.Write(" | (");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(
                    "{0}:{1}",
                    token.Span.Start.Line,
                    token.Span.Start.Column
                );
                Console.ResetColor();
                Console.Write(") -> (");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(
                    "{0}:{1}",
                    token.Span.End.Line,
                    token.Span.End.Column
                );
                Console.ResetColor();
                Console.WriteLine(")");
            }
        }
    }
}