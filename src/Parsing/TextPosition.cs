using System;

namespace Caique.Parsing
{
    struct TextPosition
    {
        public int Line { get; }

        public int Column { get; }

        public TextPosition(int line, int column)
        {
            Line = line;
            Column = column;
        }
    }
}