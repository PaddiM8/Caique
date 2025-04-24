using Caique.Analysis;
using Caique.Lexing;

namespace Caique;

public class DiagnosticReporter
{
    public IReadOnlyList<Diagnostic> Diagnostics
        => _diagnostics;

    public IEnumerable<Diagnostic> Errors
        => _diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error);

    private readonly List<Diagnostic> _diagnostics = [];

    public void ReportUnexpectedToken(Token unexpected)
    {
        ReportError($"Unexpected token {unexpected.Kind}.", unexpected.Span);
    }

    public void ReportUnexpectedToken(Token unexpected, ICollection<string> expected)
    {
        if (expected.Count == 1)
        {
            ReportError($"Unexpected token {unexpected.Kind}. Expected {expected.Single()}.", unexpected.Span);
        }
        else
        {
            ReportError($"Unexpected token {unexpected.Kind}. Expected one of: {string.Join(", ", expected)}.", unexpected.Span);
        }
    }

    public void UnrecognisedToken(string value, TextSpan span)
    {
        ReportError($"Unrecognised token '{value}'.", span);
    }

    public void ReportUnexpectedEnd(Token nearestToken)
    {
        ReportError($"Unexpected end of expression.", nearestToken.Span);
    }

    public void ReportWrongNumberOfArguments(int expected, int got, TextSpan span)
    {
        ReportError($"Wrong number of arguments. Expected {expected} but got {got}.", span);
    }

    public void ReportNotFound(Token identifier)
    {
        ReportError($"Symbol not found: '{identifier.Value}'.", identifier.Span);
    }

    public void ReportNotFound(List<Token> typeNames)
    {
        var fullName = string.Join("::", typeNames.Select(x => x.Value));
        var span = typeNames.First().Span.Combine(typeNames.Last().Span);
        ReportError($"Symbol not found: {fullName}.", span);
    }

    public void ReportSymbolAlreadyExists(Token token)
    {
        ReportError($"A symbol with the name '{token.Value}' already exists.", token.Span);
    }

    public void ReportIncompatibleType(string expected, IDataType got, TextSpan span)
    {
        ReportError($"Incompatible type. Expected {expected} but got {got}.", span);
    }

    public void ReportIncompatibleType(Primitive expected, IDataType got, TextSpan span)
    {
        var expectedDataType = new PrimitiveDataType(expected);
        ReportError($"Incompatible type. Expected {expectedDataType} but got {got}.", span);
    }

    public void ReportIncompatibleType(IDataType expected, IDataType got, TextSpan span)
    {
        ReportError($"Incompatible type. Expected {expected} but got {got}.", span);
    }

    public void ReportNonStaticSymbolReferencedAsStatic(Token identifier)
    {
        ReportError($"Non-static symbol was referenced as if it was static.", identifier.Span);
    }

    public void ReportStaticSymbolReferencedAsNonStatic(Token identifier)
    {
        ReportError($"Static symbol was referenced as if it was non-static.", identifier.Span);
    }

    public void ReportConstructorAlreadyExists(Token structureIdentifier, TextSpan span)
    {
        ReportError($"A constructor is already present in '{structureIdentifier.Value}'.", span);
    }

    public void ReportInitParameterFieldNotFound(Token identifier)
    {
        ReportError($"The constructor parameter was treated as a field reference, since it lacks a type, but a field with the name '{identifier.Value}' was not found.", identifier.Span);
    }

    private void ReportHint(string message, TextSpan span)
    {
        var diagnostic = new Diagnostic(DiagnosticSeverity.Hint, message, span);
        _diagnostics.Add(diagnostic);
    }

    private void ReportWarning(string message, TextSpan span)
    {
        var diagnostic = new Diagnostic(DiagnosticSeverity.Warning, message, span);
        _diagnostics.Add(diagnostic);
    }

    private void ReportError(string message, TextSpan span)
    {
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, message, span);
        _diagnostics.Add(diagnostic);
    }
}
