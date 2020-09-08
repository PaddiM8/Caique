using System;
using Caique.Parsing;

namespace Caique
{
    public class Compilation
    {
        public Compilation(string source)
        {
            var tokens = new Lexer().Lex(source);
        }
    }
}