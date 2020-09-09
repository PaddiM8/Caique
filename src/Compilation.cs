using System;
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
        }
    }
}