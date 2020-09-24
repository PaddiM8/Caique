using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caique.AST;
using Caique.Diagnostics;
using Caique.Parsing;
using Caique.Semantics;
using Caique.Util;

namespace Caique
{
    public class Compilation
    {
        public DiagnosticBag Diagnostics = new DiagnosticBag();
        public ModuleEnvironment Environment;

        public Compilation(ModuleEnvironment environment)
        {
            Environment = environment;

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
                    new TypeChecker(
                        ast,
                        Diagnostics
                    ).Analyse();

                    if (Program.Options!.PrintAst)
                        ast.Print();
                }
            }

            if (Program.Options!.PrintEnvironment)
                Environment.Print();

            foreach (var diagnostic in Diagnostics)
                diagnostic.Print();
        }

        private List<Ast> ParseModuleEnvironment(ModuleEnvironment environment)
        {
            return ParseModuleEnvironment(environment, new List<Ast>());
        }

        private List<Ast> ParseModuleEnvironment(ModuleEnvironment environment,
                                                 List<Ast> asts)
        {
            foreach (var (_, module) in environment.Modules)
            {
                // If the module is a directory, just parse its children and continue
                if (module.FilePath == null)
                {
                    ParseModuleEnvironment(module, asts);
                    continue;
                }

                // Lex
                var tokens = new Lexer(
                    File.ReadAllText(module.FilePath),
                    Diagnostics)
                .Lex();

                if (Program.Options!.PrintTokens)
                    PrintTokens(tokens);

                // Parse
                var ast = new Parser(
                    tokens,
                    Diagnostics,
                    module
                ).Parse();
                asts.Add(new Ast(ast, module));

                ParseModuleEnvironment(module, asts);
            }

            return asts;
        }

        private void PrintTokens(List<Token> tokens)
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