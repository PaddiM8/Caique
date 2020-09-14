using System;
using System.Collections.Generic;
using System.Linq;
using Caique.AST;
using Caique.Diagnostics;
using Caique.Parsing;

namespace Caique
{
    public class Compilation
    {
        public DiagnosticBag Diagnostics = new DiagnosticBag();

        public Compilation(string source)
        {
            var tokens = new Lexer(source, Diagnostics).Lex();

            if (Program.Options!.PrintTokens)
                PrintTokens(tokens);

            var statements = new Parser(
                tokens,
                Diagnostics,
                Environment
            ).Parse();


            if (Program.Options!.PrintAst)
                new AstPrinter().PrintStatements(statements);

            foreach (var diagnostic in Diagnostics)
                diagnostic.Print();
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