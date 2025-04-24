using Caique.Lexing;
using Caique.Scope;

namespace Caique.Analysis;

public class SemanticTree(SemanticNode root)
{
    public SemanticNode Root { get; } = root;
}

public abstract class SemanticNode(IDataType dataType, TextSpan span)
{
    public IDataType DataType { get; } = dataType;

    public TextSpan Span { get; } = span;
}

public class SemanticLiteralNode(Token value, IDataType dataType)
    : SemanticNode(dataType, value.Span)
{
    public Token Value { get; } = value;
}

public class SemanticVariableReferenceNode(Token identifier, VariableSymbol symbol)
    : SemanticNode(symbol.Declaration.DataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;
}

public class SemanticFunctionReferenceNode(Token identifier, FunctionSymbol symbol, IDataType dataType)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public FunctionSymbol Symbol { get; } = symbol;
}

public class SemanticStructureReferenceNode(Token identifier, StructureSymbol symbol, IDataType dataType)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public StructureSymbol Symbol { get; } = symbol;
}

public class SemanticFieldReferenceNode(Token identifier, FieldSymbol symbol, IDataType dataType)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public FieldSymbol Symbol { get; } = symbol;
}

public class SemanticUnaryNode(TokenKind op, SemanticNode value, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public TokenKind Operator { get; } = op;

    public SemanticNode Value { get; } = value;
}

public class SemanticBinaryNode(SemanticNode left, TokenKind op, SemanticNode right, IDataType dataType)
    : SemanticNode(dataType, left.Span.Combine(right.Span))
{
    public SemanticNode Left { get; } = left;

    public TokenKind Operator { get; } = op;

    public SemanticNode Right { get; } = right;
}

public class SemanticAssignmentNode(ISymbol leftSymbol, SemanticNode right, TextSpan span)
    : SemanticNode(right.DataType, span)
{
    public ISymbol Left { get; } = leftSymbol;

    public SemanticNode Right { get; } = right;
}

public class SemanticCallNode(SemanticNode left, List<SemanticNode> arguments, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public SemanticNode Left { get; } = left;

    public List<SemanticNode> Arguments { get; } = arguments;
}

public class SemanticNewNode(List<SemanticNode> arguments, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public List<SemanticNode> Arguments { get; } = arguments;
}

public class SemanticBlockNode(List<SemanticNode> expressions, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public List<SemanticNode> Expressions { get; } = expressions;
}

public class SemanticTypeNode(IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
}

public interface ISemanticVariableDeclaration
{
    Token Identifier { get; }

    IDataType DataType { get; }
}

public class SemanticVariableDeclarationNode(Token identifier, SemanticNode value, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span), ISemanticVariableDeclaration
{
    public Token Identifier { get; } = identifier;

    public SemanticNode Value { get; } = value;
}

public class SemanticFunctionDeclarationNode(
    Token identifier,
    List<SemanticParameterNode> parameters,
    IDataType returnType,
    SemanticBlockNode body,
    bool isStatic,
    TextSpan span
)
    : SemanticNode(new PrimitiveDataType(Primitive.Void), span)
{
    public Token Identifier { get; } = identifier;

    public List<SemanticParameterNode> Parameters { get; } = parameters;

    public IDataType ReturnType { get; } = returnType;

    public SemanticBlockNode Body { get; } = body;

    public bool IsStatic { get; } = isStatic;
}

public class SemanticParameterNode(Token identifier, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span), ISemanticVariableDeclaration
{
    public Token Identifier { get; } = identifier;
}

public class SemanticClassDeclarationNode(Token identifier, List<SemanticNode> declarations, TextSpan span)
    : SemanticNode(new PrimitiveDataType(Primitive.Void), span)
{
    public Token Identifier { get; } = identifier;

    public List<SemanticNode> Declarations { get; } = declarations;
}

public class SemanticFieldDeclarationNode(Token identifier, SemanticNode? value, bool isStatic, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public Token Identifier { get; } = identifier;

    public SemanticNode? Value { get; } = value;

    public bool IsStatic { get; } = isStatic;
}

public class SemanticInitNode(List<SemanticParameterNode> parameters, SemanticBlockNode body, TextSpan span)
    : SemanticNode(new PrimitiveDataType(Primitive.Void), span)
{
    public List<SemanticParameterNode> Parameters { get; } = parameters;

    public SemanticBlockNode Body { get; } = body;
}

public class SemanticInitParameterNode(Token identifier, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public Token Identifier { get; } = identifier;

    public StructureSymbol? ResolvedSymbol { get; set; }
}
