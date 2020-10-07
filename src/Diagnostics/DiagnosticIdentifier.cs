namespace Caique.Diagnostics
{
    /// <summary>
    /// A way to easily keep track of the type of problem.
    /// </summary>
    public enum DiagnosticIdentifier
    {
        InvalidModulePath,
        MisplacedAssignmentOperator,
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
    }
}