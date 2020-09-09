using System;
using System.Collections.Generic;
using Caique.Diagnostics;
using Caique.Parsing;

namespace Caique.Parsing
{
    class Lexer
    {
        private readonly string _source;
        private readonly DiagnosticBag _diagnostics;

        public Lexer(string source, DiagnosticBag diagnostics)
        {
            _source = source;
            _diagnostics = diagnostics;
        }

        public List<Token> Lex()
        {
            return new List<Token>();
        }
    }
}