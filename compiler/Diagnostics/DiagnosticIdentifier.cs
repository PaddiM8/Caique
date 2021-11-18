namespace Caique.Diagnostics
{
    /// <summary>
    /// A way to easily keep track of the type of problem.
    /// </summary>
    public enum DiagnosticIdentifier
    {
        CannotOverrideNonVirtual,
        CanOnlyHaveOneConstructor,
        InvalidCharacterLiteral,
        InvalidModulePath,
        ExpectedOverride,
        ExpectedSuper,
        MisplacedAssignmentOperator,
        MisplacedOverride,
        MisplacedSelfKeyword,
        MisplacedSuperKeywordWithArguments,
        MisplacedVirtual,
        SymbolAlreadyExists,
        SymbolDoesNotExist,
        UnableToInferType,
        UnableToInherit,
        UnexpectedToken,
        UnexpectedType,
        UnterminatedStringLiteral,
        UnknownEscapeSequence,
        UnknownToken,
        WrongNumberOfArguments,
        WrongNumberOfTypeArguments,
    }
}