namespace Caique.Parsing;

public class ParserRecoveryException(SyntaxErrorNode errorNode) : Exception
{
    public SyntaxErrorNode ErrorNode { get; } = errorNode;
}
