using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.Diagnostics
{
    /// <summary>
    /// List of `Diagnostic` objects.
    /// Use the functions here to log new errors.
    /// </summary>
    public class DiagnosticBag : IEnumerable<Diagnostic>
    {
        private readonly List<Diagnostic> _diagnostics = new();

        public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();

        public string CurrentFile { get; set; } = "";

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void AddRange(IEnumerable<Diagnostic> diagnostics)
        {
            _diagnostics.AddRange(diagnostics);
        }

        public void ReportCannotOverrideNonVirtual(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.CannotOverrideNonVirtual,
                $"Only functions marked as virtual may be overridden.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportCanOnlyHaveOneConstructor(Token initToken)
        {
            Report(
                DiagnosticIdentifier.CanOnlyHaveOneConstructor,
                $"An object can only contain one constructor (init).",
                initToken.Span,
                DiagnosticType.Error
            );
        }

        public void ReportInvalidModulePath(List<Token> identifiers)
        {
            var path = string.Join("->", identifiers.Select(x => x.Value));
            Report(
                DiagnosticIdentifier.InvalidModulePath,
                $"The module or symbol '{path}' does not exist.",
                identifiers[0].Span.Add(identifiers[^1].Span),
                DiagnosticType.Error
            );
        }

        public void ReportInvalidCharacterLiteral(TextPosition position)
        {
            Report(
                DiagnosticIdentifier.InvalidCharacterLiteral,
                $"Invalid character literal. A character literal may only contain one character.",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        public void ReportExpectedOverride(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.ExpectedSuper,
                "Expected 'override' keyword on function since a function with the same name already exists in an inherited class.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportExpectedSuper(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.ExpectedSuper,
                "Expected a super call to the constructor of the inherited class.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportMisplacedAssignmentOperator(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.MisplacedAssignmentOperator,
                $"Misplaced comparison operator. Expected the left-hand expression to be a variable.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportMisplacedOverride(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.MisplacedSuperKeywordWithArguments,
                $"Misplaced 'override' keyword. The 'override' keyword may only be used on functions belonging to a class.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportMisplacedSelfKeyword(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.MisplacedAssignmentOperator,
                $"Misplaced 'self' keyword. The 'self' keyword can only be used inside objects and extension functions.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportMisplacedSuperKeywordWithArguments(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.MisplacedSuperKeywordWithArguments,
                $"Misplaced 'super' keyword. The 'super' keyword may only be used with arguments inside a constructor of a class that inherits from a class that uses a constructor.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportMisplacedVirtual(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.MisplacedSuperKeywordWithArguments,
                $"Misplaced 'virtual' keyword. The 'virtual' keyword may only be used on functions belonging to a class.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportSymbolAlreadyExists(Token identifier)
        {
            Report(
                DiagnosticIdentifier.SymbolAlreadyExists,
                $"A symbol with the name {identifier.Value} already exists in the current scope.",
                identifier.Span,
                DiagnosticType.Error
            );
        }

        public void ReportSymbolDoesNotExist(Token identifier)
        {
            Report(
                DiagnosticIdentifier.SymbolDoesNotExist,
                $"A symbol with the name '{identifier.Value}' does not exist.",
                identifier.Span,
                DiagnosticType.Error
            );
        }

        public void ReportUnableToInferType(TextSpan span)
        {
            Report(
                DiagnosticIdentifier.UnableToInferType,
                $"Unable to infer type.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportUnableToInherit(Token baseType, Token inheritor)
        {
            Report(
                DiagnosticIdentifier.UnableToInherit,
                $"Unable to inherit from '${baseType}' in object '${inheritor}'.",
                baseType.Span.Add(inheritor.Span),
                DiagnosticType.Error
            );
        }

        public void ReportUnexpectedToken(Token got, string expected)
        {
            Report(
                DiagnosticIdentifier.UnexpectedToken,
                $"Unexpected token '{got.Kind.ToStringRepresentation()}', expected {expected}.",
                got.Span,
                DiagnosticType.Error
            );
        }

        public void ReportUnexpectedToken(Token got, params TokenKind[] expected)
        {
            var expectedString = new StringBuilder();

            for (int i = 0; i < expected.Length; i++)
            {
                // If last one, prepend "or" unless it's the only one
                if (i == expected.Length - 1)
                {
                    if (expected.Length >= 2)
                        expectedString.Append(" or ");
                }

                expectedString.Append($"'{expected[i].ToStringRepresentation()}'");

                // If not one of the last two
                if (i < expected.Length - 2)
                {
                    expectedString.Append(',');
                }
            }

            Report(
                DiagnosticIdentifier.UnexpectedToken,
                $"Unexpected token '{got.Kind.ToStringRepresentation()}', expected {expectedString}.",
                got.Span,
                DiagnosticType.Error
            );
        }

        public void ReportUnexpectedType(IDataType got, IDataType expected, TextSpan span)
        {
            Report(
                DiagnosticIdentifier.UnexpectedType,
                $"Unexpected type '{got}', expected '{expected}'.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportUnexpectedType(IDataType got, string expected, TextSpan span)
        {
            Report(
                DiagnosticIdentifier.UnexpectedType,
                $"Unexpected type '{got}', expected '{expected}'.",
                span,
                DiagnosticType.Error
            );
        }

        public void ReportUnterminatedStringLiteral(TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnterminatedStringLiteral,
                "Unterminated string literal",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        public void ReportUnknownEscapeSequence(string escaped, TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnknownEscapeSequence,
                $"Unknown escape sequence '\\{escaped}'.",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        public void ReportUnknownToken(string tokenValue, TextPosition position)
        {
            Report(
                DiagnosticIdentifier.UnknownToken,
                $"Unknown token '{tokenValue}'.",
                new TextSpan(position, position),
                DiagnosticType.Error
            );
        }

        public void ReportWrongNumberOfArguments(Token identifier, int got, int expected)
        {
            Report(
                DiagnosticIdentifier.WrongNumberOfArguments,
                $"Wrong number of arguments for function '{identifier.Value}'. Got {got}, but expected {expected}.",
                identifier.Span,
                DiagnosticType.Error
            );
        }

        public void ReportWrongNumberOfTypeArguments(Token identifier, int got, int expected)
        {
            Report(
                DiagnosticIdentifier.WrongNumberOfArguments,
                $"Wrong number of type arguments for '{identifier.Value}'. Got {got}, but expected {expected}.",
                identifier.Span,
                DiagnosticType.Error
            );
        }

        private void Report(DiagnosticIdentifier identifier,
                            string message,
                            TextSpan span,
                            DiagnosticType type)
        {
            _diagnostics.Add(new Diagnostic(
                identifier,
                message,
                span,
                type,
                CurrentFile
            ));
        }
    }
}