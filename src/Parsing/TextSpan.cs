using System;

namespace Caique.Parsing
{
    public record TextSpan
    {
        public TextPosition Start { get; }

        public TextPosition End { get; }

        public TextSpan(TextPosition start, TextPosition end)
        {
            Start = start;
            End = end;
        }

        public TextSpan Add(TextSpan span2)
        {
            return new TextSpan(
                new TextPosition(Start.Line, Start.Column),
                new TextPosition(span2.End.Line, span2.End.Column)
            );
        }
    }
}