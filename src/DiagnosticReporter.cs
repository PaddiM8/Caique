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

    public void ReportNotFound(Token identifier, string context)
    {
        ReportError($"Symbol not found in {context}: '{identifier.Value}'.", identifier.Span);
    }

    public void ReportNotFound(List<Token> typeNames)
    {
        var fullName = string.Join(":", typeNames.Select(x => x.Value));
        var span = typeNames.First().Span.Combine(typeNames.Last().Span);
        ReportError($"Symbol not found: {fullName}.", span);
    }

    public void ReportDuplicateEntry(Token identifier, Token otherIdentifier)
    {
        ReportError($"Duplicate entry found: '{identifier.Value}'.", identifier.Span);
        ReportHint($"Other duplicate defined here.", otherIdentifier.Span);
    }

    public void ReportBodyInProtocol(TextSpan span)
    {
        ReportError($"Bodies in function declarations are not allowed in protocols.", span);
    }

    public void ReportInvalidNamespace(List<Token> path)
    {
        var namespaceString = string.Join(":", path.Select(x => x.Value));
        var span = path
            .First()
            .Span
            .Combine(path.Last().Span);

        ReportError($"Invalid namespace: {namespaceString}.", span);
    }

    public void ReportMemberNotFound(Token identifier, IDataType dataType)
    {
        ReportError($"Member '{identifier.Value}' not found in '{dataType}'.", identifier.Span);
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

    public void ReportInvalidCast(IDataType fromType, IDataType toType, TextSpan span)
    {
        ReportError($"Invalid cast. Cannot cast from {fromType} to {toType}.", span);
    }

    public void ReportExpectedVariableReferenceInAssignment(TextSpan span)
    {
        ReportError($"Expected variable reference in the left side of the assignment.", span);
    }

    public void ReportNonStaticSymbolReferencedAsStatic(Token identifier)
    {
        ReportError("Non-static symbol was referenced as if it was static.", identifier.Span);
    }

    public void ReportStaticSymbolReferencedAsNonStatic(Token identifier)
    {
        ReportError("Static symbol was referenced as if it was non-static.", identifier.Span);
    }

    public void ReportNonStaticFunctionReferenceMustBeCalled(Token identifier)
    {
        ReportError("Non-static function reference must be called.", identifier.Span);
    }

    public void ReportNonStaticMainFunction(TextSpan span)
    {
        ReportError("Main function must be static.", span);
    }

    public void ReportNonConstantValueInStaticField(TextSpan span)
    {
        ReportError($"Static fields can only be initialised with constant values.", span);
    }

    public void ReportConstructorAlreadyExists(Token structureIdentifier, TextSpan span)
    {
        ReportError($"A constructor is already present in '{structureIdentifier.Value}'.", span);
    }

    public void ReportInitParameterFieldNotFound(Token identifier)
    {
        ReportError($"The constructor parameter was treated as a field reference, since it lacks a type, but a field with the name '{identifier.Value}' was not found.", identifier.Span);
    }

    public void ReportReturnOutsideFunction(TextSpan span)
    {
        ReportError("A return statement must be placed within a function.", span);
    }

    public void ReportMultipleInheritance(TextSpan span)
    {
        ReportError("Inheriting from multiple classes is not allowed.", span);
    }

    public void ReportMisplacedBaseCall(TextSpan span)
    {
        ReportError("A base call must be at the top of a constructor of a class that inherits from another class.", span);
    }

    public void ReportFunctionNameSameAsParentStructure(Token identifier)
    {
        ReportError($"Function name cannot be the same as its parent structure ('{identifier.Value}').", identifier.Span);
    }

    public void ReportBaseFunctionNotFound(Token identifier)
    {
        ReportError($"Unable to override: a function with the name {identifier.Value} found in the base class.", identifier.Span);
    }

    public void ReportBaseClassIsNotInheritable(TextSpan functionSpan, TextSpan baseClassSpan)
    {
        ReportError($"Unable to override: Base class is not inheritable.", functionSpan);
        ReportHint("Use the 'inheritable' keyword to enable overriding.", baseClassSpan);
    }

    public void ReportInvalidEnumType(IDataType dataType, TextSpan span)
    {
        ReportError($"Invalid enum type: {dataType}. Enums can only have primitive or string values", span);
    }

    public void ReportInvalidEnumMemberValue(string details, TextSpan span)
    {
        ReportError($"Invalid enum member value: {details}.", span);
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
