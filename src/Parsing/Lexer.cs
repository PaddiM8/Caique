using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Parsing
{
    class Lexer
    {
        private string _source;

        public List<Token> Lex(string source)
        {
            _source = source;

            return new List<Token>();
        }
    }
}