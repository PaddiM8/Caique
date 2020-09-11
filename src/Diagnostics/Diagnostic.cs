using System;
using Caique.Parsing;

namespace Caique.Diagnostics
{
    public class Diagnostic
    {
        public DiagnosticIdentifier Identifier { get; }

        public string Message { get; }

        public TextSpan Span { get; }

        public DiagnosticType Type { get; }

        public Diagnostic(DiagnosticIdentifier identifier, string message, TextSpan span, DiagnosticType type)
        {
            Identifier = identifier;
            Message = message;
            Span = span;
            Type = type;
        }

        public void Print()
        {
            Console.ForegroundColor = Type switch
            {
                DiagnosticType.Error => ConsoleColor.Red,
                DiagnosticType.Warning => ConsoleColor.Yellow,
                _ => throw new NotImplementedException(),
            };

            Console.Write(Type.ToString());
            Console.ResetColor();
            Console.Write(": ");
            Console.WriteLine(Message);
        }
    }
}