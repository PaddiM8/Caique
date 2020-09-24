using System;
using Caique.Parsing;

namespace Caique.Diagnostics
{
    /// <summary>
    /// An error/warning/etc. found by the compiler.
    /// </summary>
    public class Diagnostic
    {
        public DiagnosticIdentifier Identifier { get; }

        public string Message { get; }

        public TextSpan Span { get; }

        public DiagnosticType Type { get; }

        public string File { get; }

        public Diagnostic(DiagnosticIdentifier identifier, string message,
                          TextSpan span, DiagnosticType type, string file)
        {
            Identifier = identifier;
            Message = message;
            Span = span;
            Type = type;
            File = file;
        }

        public void Print()
        {
            // Print position
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"[{Span.Start.Line}:{Span.Start.Column}] ");
            Console.Write(File + ": ");

            // Print "Error"/"Warning"
            Console.ForegroundColor = Type switch
            {
                DiagnosticType.Error => ConsoleColor.Red,
                DiagnosticType.Warning => ConsoleColor.Yellow,
                _ => throw new NotImplementedException(),
            };

            Console.Write(Type.ToString());
            Console.ResetColor();
            Console.Write(": ");

            // Print error message
            Console.WriteLine(Message);
        }
    }
}