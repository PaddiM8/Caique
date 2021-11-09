using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Caique.Ast;
using Caique.Semantics;

namespace Caique.Printing
{
    class ObjectPrinter
    {
        public static void PrintTokens(ModuleEnvironment environment)
        {
            foreach (var (_, child) in environment.Modules)
            {
                PrintTokens(child);
            }

            if (environment.Tokens == null) return;

            PrintIdentifier(environment.Identifier);
            foreach (var token in environment.Tokens)
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

            Console.WriteLine();
        }

        public static void PrintAst(ModuleEnvironment environment)
        {
            foreach (var (_, child) in environment.Modules)
            {
                PrintAst(child);
            }

            if (environment.Ast != null)
            {
                PrintIdentifier(environment.Identifier);
                AstPrinter.Print(environment.Ast);
                Console.WriteLine();
            }
        }

        public static void PrintSymbols(ModuleEnvironment environment)
        {
            EnvironmentPrinter.Print(environment);
        }

        private static void PrintIdentifier(string identifier)
        {
            Console.WriteLine($"{identifier}:");
            Console.WriteLine(string.Join("", Enumerable.Repeat('-', identifier.Length)));
        }
    }
}