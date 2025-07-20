using Caique.Analysis;
using Caique.Lexing;

namespace Caique;

public enum DiagnosticCode
{
    // Hints
    HintColonInsteadOfDot,
    HintOtherDuplicate,
    HintUseInheritableKeyword,
    HintAssigneeHere,
    HintChangeToVar,

    // Errors
    ErrorCompilerError,
    ErrorUnexpectedToken,
    ErrorUnrecognisedToken,
    ErrorUnexpectedEnd,
    ErrorWrongNumberOfArguments,
    ErrorExpectedType,
    ErrorExpectedValueGotType,
    ErrorNotFound,
    ErrorTypeNotFound,
    ErrorDuplicateEntry,
    ErrorBodyInProtocol,
    ErrorInvalidNamespace,
    ErrorMemberNotFound,
    ErrorSymbolAlreadyExists,
    ErrorInvalidSymbolName,
    ErrorIncompatibleType,
    ErrorInvalidCast,
    ErrorExpectedVariableReferenceInAssignment,
    ErrorNonStaticSymbolReferencedAsStatic,
    ErrorStaticSymbolReferencedAsNonStatic,
    ErrorNonStaticFunctionReferenceMustBeCalled,
    ErrorNonStaticMainFunction,
    ErrorConstructorAlreadyExists,
    ErrorInitParameterFieldNotFound,
    ErrorReturnOutsideFunction,
    ErrorMultipleInheritance,
    ErrorMisplacedSelf,
    ErrorMisplacedBase,
    ErrorMisplacedBaseCall,
    ErrorFunctionNameSameAsParentStructure,
    ErrorBaseFunctionNotFound,
    ErrorBaseClassNotInheritable,
    ErrorInvalidEnumType,
    ErrorInvalidEnumMemberValue,
    ErrorAssignmentToImmutable,
    ErrorMutablePropertyWithoutSetter,
    ErrorSetterButNoGetter,
    ErrorSetterOnImmutable,
    ErrorPublicMutableStaticField,
    ErrorSymbolIsPrivate,
}

public class DiagnosticReporter
{
    public IReadOnlyList<Diagnostic> Diagnostics
        => _diagnostics;

    public IEnumerable<Diagnostic> Errors
        => _diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error);

    private readonly List<Diagnostic> _diagnostics = [];

    public void HintColonInsteadOfDot(TextSpan span)
    {
        ReportHint(
            DiagnosticCode.HintColonInsteadOfDot,
            $"Did you mean to use a colon instead of a dot?",
            span
        );
    }

    public void ReportCompilerError()
    {
        ReportError(DiagnosticCode.ErrorCompilerError, $"Internal compiler error.", null);
    }

    public void ReportUnexpectedToken(Token unexpected)
    {
        ReportError(
            DiagnosticCode.ErrorUnexpectedToken,
            $"Unexpected token {unexpected.Kind}.",
            unexpected.Span
        );
    }

    public void ReportUnexpectedToken(Token unexpected, ICollection<string> expected)
    {
        if (expected.Count == 1)
        {
            ReportError(
                DiagnosticCode.ErrorUnexpectedToken,
                $"Unexpected token {unexpected.Kind}. Expected {expected.Single()}.",
                unexpected.Span
            );
        }
        else
        {
            ReportError(
                DiagnosticCode.ErrorUnexpectedToken,
                $"Unexpected token {unexpected.Kind}. Expected one of: {string.Join(", ", expected)}.",
                unexpected.Span
            );
        }
    }

    public void ReportUnrecognisedToken(string value, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorUnrecognisedToken,
            $"Unrecognised token '{value}'.",
            span
        );
    }

    public void ReportUnexpectedEnd(Token nearestToken)
    {
        ReportError(
            DiagnosticCode.ErrorUnexpectedEnd,
            $"Unexpected end of expression.",
            nearestToken.Span
        );
    }

    public void ReportWrongNumberOfArguments(int expected, int got, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorWrongNumberOfArguments,
            $"Wrong number of arguments. Expected {expected} but got {got}.",
            span
        );
    }

    public void ReportExpectedType(TextSpan span, string got)
    {
        ReportError(DiagnosticCode.ErrorExpectedType, $"Expected type but got {got}.", span);
    }

    public void ReportExpectedValueGotType(Token token)
    {
        ReportError(
            DiagnosticCode.ErrorExpectedValueGotType,
            $"Expected value but got type: '{token.Value}'.",
            token.Span
        );
    }

    public void ReportNotFound(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorNotFound,
            $"Symbol not found: '{identifier.Value}'.",
            identifier.Span
        );
    }

    public void ReportNotFound(Token identifier, string context)
    {
        ReportError(
            DiagnosticCode.ErrorNotFound,
            $"Symbol not found in {context}: '{identifier.Value}'.",
            identifier.Span
        );
    }

    public void ReportNotFound(List<Token> typeNames)
    {
        var fullName = string.Join(":", typeNames.Select(x => x.Value));
        var span = typeNames.First().Span.Combine(typeNames.Last().Span);
        ReportError(DiagnosticCode.ErrorNotFound, $"Symbol not found: {fullName}.", span);
    }

    public void ReportTypeNotFound(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorTypeNotFound,
            $"Type not found: '{identifier.Value}'.",
            identifier.Span
        );
    }

    public void ReportTypeNotFound(List<Token> typeNames)
    {
        var fullName = string.Join(":", typeNames.Select(x => x.Value));
        var span = typeNames.First().Span.Combine(typeNames.Last().Span);
        ReportError(DiagnosticCode.ErrorTypeNotFound, $"Type not found: {fullName}.", span);
    }

    public void ReportDuplicateEntry(Token identifier, Token otherIdentifier)
    {
        ReportError(DiagnosticCode.ErrorDuplicateEntry, $"Duplicate entry found: '{identifier.Value}'.", identifier.Span);
        ReportHint(DiagnosticCode.HintOtherDuplicate, $"Other duplicate defined here.", otherIdentifier.Span);
    }

    public void ReportBodyInProtocol(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorBodyInProtocol,
            $"Bodies in function declarations are not allowed in protocols.",
            span
        );
    }

    public void ReportInvalidNamespace(List<Token> path)
    {
        var namespaceString = string.Join(":", path.Select(x => x.Value));
        var span = path
            .First()
            .Span
            .Combine(path.Last().Span);

        ReportError(DiagnosticCode.ErrorInvalidNamespace, $"Invalid namespace: {namespaceString}.", span);
    }

    public void ReportMemberNotFound(Token identifier, IDataType dataType)
    {
        ReportError(
            DiagnosticCode.ErrorMemberNotFound,
            $"Member '{identifier.Value}' not found in '{dataType}'.",
            identifier.Span
        );
    }

    public void ReportSymbolAlreadyExists(Token token)
    {
        ReportError(
            DiagnosticCode.ErrorSymbolAlreadyExists,
            $"A symbol with the name '{token.Value}' already exists.",
            token.Span
        );
    }

    public void ReportInvalidSymbolName(Token token)
    {
        ReportError(
            DiagnosticCode.ErrorInvalidSymbolName,
            $"The name '{token.Value}' is invalid/reserved in the current context.",
            token.Span
        );
    }

    public void ReportIncompatibleType(string expected, IDataType got, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorIncompatibleType,
            $"Incompatible type. Expected {expected} but got {got}.",
            span
        );
    }

    public void ReportIncompatibleType(Primitive expected, IDataType got, TextSpan span)
    {
        var expectedDataType = new PrimitiveDataType(expected);
        ReportError(
            DiagnosticCode.ErrorIncompatibleType,
            $"Incompatible type. Expected {expectedDataType} but got {got}.",
            span
        );
    }

    public void ReportIncompatibleType(IDataType expected, IDataType got, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorIncompatibleType,
            $"Incompatible type. Expected {expected} but got {got}.",
            span
        );
    }

    public void ReportInvalidCast(IDataType fromType, IDataType toType, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorInvalidCast,
            $"Invalid cast. Cannot cast from {fromType} to {toType}.",
            span
        );
    }

    public void ReportExpectedVariableReferenceInAssignment(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorExpectedVariableReferenceInAssignment,
            $"Expected variable reference in the left side of the assignment.",
            span
        );
    }

    public void ReportNonStaticSymbolReferencedAsStatic(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorNonStaticSymbolReferencedAsStatic,
            "Non-static symbol was referenced as if it was static.",
            identifier.Span
        );
    }

    public void ReportStaticSymbolReferencedAsNonStatic(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorStaticSymbolReferencedAsNonStatic,
            "Static symbol was referenced as if it was non-static.",
            identifier.Span
        );
    }

    public void ReportNonStaticFunctionReferenceMustBeCalled(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorNonStaticFunctionReferenceMustBeCalled,
            "Non-static function reference must be called.",
            identifier.Span
        );
    }

    public void ReportNonStaticMainFunction(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorNonStaticMainFunction,
            "Main:Run function must be static.",
            span
        );
    }

    public void ReportConstructorAlreadyExists(Token structureIdentifier, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorConstructorAlreadyExists,
            $"A constructor is already present in '{structureIdentifier.Value}'.",
            span
        );
    }

    public void ReportInitParameterFieldNotFound(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorInitParameterFieldNotFound,
            $"The constructor parameter was treated as a field reference, since it lacks a type, but a field with the name '{identifier.Value}' was not found.",
            identifier.Span
        );
    }

    public void ReportReturnOutsideFunction(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorReturnOutsideFunction,
            "A return statement must be placed within a function.",
            span
        );
    }

    public void ReportMultipleInheritance(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorMultipleInheritance,
            "Inheriting from multiple classes is not allowed.",
            span
        );
    }

    public void ReportMisplacedSelf(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorMisplacedSelf,
            "A 'self' keyword must be placed within a non-static member function.",
            span
        );
    }

    public void ReportMisplacedBase(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorMisplacedBase,
            "A 'base' keyword must be placed within a non-static member function of an inheriting structure.",
            span
        );
    }

    public void ReportMisplacedBaseCall(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorMisplacedBaseCall,
            "A base call must be at the top of a constructor of a class that inherits from another class.",
            span
        );
    }

    public void ReportFunctionNameSameAsParentStructure(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorFunctionNameSameAsParentStructure,
            $"Function name cannot be the same as its parent structure ('{identifier.Value}').",
            identifier.Span
        );
    }

    public void ReportBaseFunctionNotFound(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorBaseFunctionNotFound,
            $"Unable to override: a function with the name {identifier.Value} found in the base class.",
            identifier.Span
        );
    }

    public void ReportBaseClassNotInheritable(TextSpan functionSpan, TextSpan baseClassSpan)
    {
        ReportError(
            DiagnosticCode.ErrorBaseClassNotInheritable,
            $"Unable to override: Base class is not inheritable.",
            functionSpan
        );
        ReportHint(
            DiagnosticCode.HintUseInheritableKeyword,
            "Use the 'inheritable' keyword to enable overriding.",
            baseClassSpan
        );
    }

    public void ReportInvalidEnumType(IDataType dataType, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorInvalidEnumType,
            $"Invalid enum type: {dataType}. Enums can only have primitive or string values",
            span
        );
    }

    public void ReportInvalidEnumMemberValue(string details, TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorInvalidEnumMemberValue,
            $"Invalid enum member value: {details}.",
            span
        );
    }

    public void ReportAssignmentToImmutable(TextSpan assignmentSpan, TextSpan declarationSpan)
    {
        ReportError(
            DiagnosticCode.ErrorAssignmentToImmutable,
            "Cannot assign to immutable value.",
            assignmentSpan
        );
        ReportHint(
            DiagnosticCode.HintAssigneeHere,
            "The assignee is defined here.",
            declarationSpan
        );
    }

    public void ReportMutablePropertyWithoutSetter(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorMutablePropertyWithoutSetter,
            "A mutable field with a getter must also have a setter. Add a setter or make it immutable.",
            span
        );
    }

    public void ReportSetterButNoGetter(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorSetterButNoGetter,
            "A field with a setter must also have a getter.",
            span
        );
    }

    public void ReportSetterOnImmutable(TextSpan setterSpan, TextSpan keywordSpan)
    {
        ReportError(
            DiagnosticCode.ErrorSetterOnImmutable,
            "A field with a setter must be mutable.",
            setterSpan
        );
        ReportHint(
            DiagnosticCode.HintChangeToVar,
            "Change from 'let' to 'var' here.",
            keywordSpan
        );
    }

    public void ReportPublicMutableStaticField(TextSpan span)
    {
        ReportError(
            DiagnosticCode.ErrorPublicMutableStaticField,
            "A mutable static field may not be public. Make the field private or immutable.",
            span
        );
    }

    public void ReportSymbolIsPrivate(Token identifier)
    {
        ReportError(
            DiagnosticCode.ErrorSymbolIsPrivate,
            $"Symbol is private: '{identifier.Value}'.",
            identifier.Span
        );
    }

    private void ReportHint(DiagnosticCode code, string message, TextSpan span)
    {
        var diagnostic = new Diagnostic(code, DiagnosticSeverity.Hint, message, span);
        _diagnostics.Add(diagnostic);
    }

    private void ReportWarning(DiagnosticCode code, string message, TextSpan span)
    {
        var diagnostic = new Diagnostic(code, DiagnosticSeverity.Warning, message, span);
        _diagnostics.Add(diagnostic);
    }

    private void ReportError(DiagnosticCode code, string message, TextSpan? span)
    {
        var diagnostic = new Diagnostic(code, DiagnosticSeverity.Error, message, span);
        _diagnostics.Add(diagnostic);
    }
}
