using System;

namespace Caique.Parsing
{
    public struct TextSpan
    {
        public TextPosition Start { get; }

        public TextPosition End { get; }

        public TextSpan(TextPosition start, TextPosition end)
        {
            Start = start;
            End = end;
        }
    }
}