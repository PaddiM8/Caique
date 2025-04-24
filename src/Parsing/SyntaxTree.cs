using Caique.Analysis;
using Caique.Lexing;
using Caique.Scope;

namespace Caique.Parsing;

public class SyntaxTree(string text, FileScope fileScope)
{
    public string Text { get; } = text;

    public NamespaceScope Namespace { get; } = fileScope.Namespace;

    public FileScope File { get; } = fileScope;

    public SyntaxNode? Root { get; private set; }

    public void Initialise(SyntaxNode root)
    {
        if (Root != null)
            throw new InvalidOperationException("Syntax tree has already been initialised.");

        Root = root;
    }

    public SyntaxBlockNode? GetEnclosingBlock(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not (null or SyntaxBlockNode))
            current = current.Parent;

        return current as SyntaxBlockNode;
    }

    public StructureScope? GetStructureScope(SyntaxNode node)
    {
        var current = node;
        while (current is not (null or ISyntaxStructure))
            current = current.Parent;

        return (current as ISyntaxStructure)?.Scope;
    }

    public LocalScope? GetLocalScope(SyntaxNode node)
    {
        var current = node;
        while (current is not (null or SyntaxBlockNode))
            current = current.Parent;

        return (current as SyntaxBlockNode)?.Scope as LocalScope;
    }
}

public abstract class SyntaxNode(TextSpan span)
{
    public TextSpan Span { get; } = span;

    public SyntaxNode? Parent { get; set; }
}

public class SyntaxErrorNode(TextSpan span)
    : SyntaxNode(span)
{
}

public class SyntaxStatementNode(SyntaxNode expression, bool isReturnValue)
    : SyntaxNode(expression.Span)
{
    public SyntaxNode Expression { get; } = expression;

    public bool IsReturnValue { get; } = isReturnValue;
}

public class SyntaxLiteralNode(Token value)
    : SyntaxNode(value.Span)
{
    public Token Value { get; } = value;
}

public class SyntaxIdentifierNode(List<Token> identifierList)
    : SyntaxNode(identifierList.First().Span.Combine(identifierList.Last().Span))
{
    public List<Token> IdentifierList { get; } = identifierList;
}

public class SyntaxUnaryNode(TokenKind op, SyntaxNode value, TextSpan span)
    : SyntaxNode(span)
{
    public TokenKind Operator { get; } = op;

    public SyntaxNode Value { get; } = value;
}

public class SyntaxBinaryNode(SyntaxNode left, TokenKind op, SyntaxNode right)
    : SyntaxNode(left.Span.Combine(right.Span))
{
    public SyntaxNode Left { get; } = left;

    public TokenKind Operator { get; } = op;

    public SyntaxNode Right { get; } = right;
}

public class SyntaxAssignmentNode(SyntaxNode left, SyntaxNode right)
    : SyntaxNode(left.Span.Combine(right.Span))
{
    public SyntaxNode Left { get; } = left;

    public SyntaxNode Right { get; } = right;
}

public class SyntaxCallNode(SyntaxNode left, List<SyntaxNode> arguments, TextSpan span)
    : SyntaxNode(span)
{
    public SyntaxNode Left { get; } = left;

    public List<SyntaxNode> Arguments { get; } = arguments;
}

public class SyntaxNewNode(SyntaxTypeNode identifier, List<SyntaxNode> arguments, TextSpan span)
    : SyntaxNode(span)
{
    public SyntaxTypeNode Type { get; } = identifier;

    public List<SyntaxNode> Arguments { get; } = arguments;
}

public class SyntaxBlockNode(List<SyntaxNode> expressions, TextSpan span)
    : SyntaxNode(span)
{
    public List<SyntaxNode> Expressions { get; } = expressions;

    public IScope? Scope { get; set; }
}

public class SyntaxParameterNode(Token identifier, SyntaxTypeNode type, TextSpan span)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public SyntaxTypeNode Type { get; } = type;
}

public class SyntaxTypeNode(List<Token> typeNames)
    : SyntaxNode(typeNames.First().Span.Combine(typeNames.Last().Span))
{
    public List<Token> TypeNames { get; } = typeNames;

    public StructureSymbol? ResolvedSymbol { get; set; }
}

public class SyntaxVariableDeclarationNode(Token identifier, SyntaxTypeNode? type, SyntaxNode value, TextSpan span)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public SyntaxTypeNode? Type { get; } = type;

    public SyntaxNode Value { get; } = value;
}

public class SyntaxFunctionDeclarationNode(
    Token identifier,
    List<SyntaxParameterNode> parameters,
    SyntaxTypeNode? returnType,
    SyntaxBlockNode body,
    bool isStatic,
    TextSpan span
)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public List<SyntaxParameterNode> Parameters { get; } = parameters;

    public SyntaxTypeNode? ReturnType { get; } = returnType;

    public SyntaxBlockNode Body { get; } = body;

    public bool IsStatic { get; } = isStatic;

    public FunctionSymbol? Symbol { get; set; }
}

public interface ISyntaxStructure
{
    Token Identifier { get; }

    StructureScope Scope { get; }
}

public class SyntaxClassDeclarationNode(
    Token identifier,
    SyntaxInitNode? constructor,
    List<SyntaxNode> declarations,
    StructureScope scope,
    TextSpan span
)
    : SyntaxNode(span), ISyntaxStructure
{
    public Token Identifier { get; } = identifier;

    public SyntaxInitNode? Constructor { get; } = constructor;

    public List<SyntaxNode> Declarations { get; } = declarations;

    public StructureScope Scope { get; } = scope;

    public StructureSymbol? Symbol { get; set; }
}

public class SyntaxFieldDeclarationNode(Token identifier, SyntaxTypeNode type, SyntaxNode? value, bool isStatic, TextSpan span)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public SyntaxTypeNode Type { get; } = type;

    public SyntaxNode? Value { get; } = value;

    public bool IsStatic { get; } = isStatic;
}

public class SyntaxInitNode(List<SyntaxInitParameterNode> parameters, SyntaxBlockNode body, TextSpan span)
    : SyntaxNode(span)
{
    public List<SyntaxInitParameterNode> Parameters { get; } = parameters;

    public SyntaxBlockNode Body { get; } = body;
}

public class SyntaxInitParameterNode(Token identifier, SyntaxTypeNode? type)
    : SyntaxNode(identifier.Span.Combine(type?.Span ?? identifier.Span))
{
    public Token Identifier { get; } = identifier;

    public SyntaxTypeNode? Type { get; } = type;

    public SyntaxFieldDeclarationNode? LinkedField { get; set; }
}
