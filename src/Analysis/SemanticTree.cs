using Caique.Lexing;
using Caique.Scope;

namespace Caique.Analysis;

public class SemanticTree(SemanticNode root)
{
    public SemanticNode Root { get; } = root;

    public SemanticBlockNode? GetEnclosingBlock(SemanticNode node)
    {
        var current = node.Parent;
        while (current is not (null or SemanticBlockNode))
            current = current.Parent;

        return current as SemanticBlockNode;
    }

    public SemanticFunctionDeclarationNode? GetEnclosingFunction(SemanticNode node)
    {
        var current = node;
        while (current is not (null or SemanticFunctionDeclarationNode))
            current = current.Parent;

        return current as SemanticFunctionDeclarationNode;
    }

    public ISemanticStructureDeclaration? GetEnclosingStructure(SemanticNode node)
    {
        var current = node;
        while (current is not (null or ISemanticStructureDeclaration))
            current = current.Parent;

        return current as ISemanticStructureDeclaration;
    }
}

public abstract class SemanticNode(IDataType dataType, TextSpan span)
{
    public IDataType DataType { get; } = dataType;

    public TextSpan Span { get; } = span;

    public SemanticNode? Parent { get; set; }

    public abstract void Traverse(Action<SemanticNode, SemanticNode> callback);
}

public class SemanticLiteralNode(Token value, IDataType dataType)
    : SemanticNode(dataType, value.Span)
{
    public Token Value { get; } = value;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
    }
}

public class SemanticVariableReferenceNode(Token identifier, VariableSymbol symbol)
    : SemanticNode(symbol.SemanticDeclaration.DataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public VariableSymbol Symbol { get; } = symbol;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
    }
}

public class SemanticFunctionReferenceNode(Token identifier, FunctionSymbol symbol, IDataType dataType)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public FunctionSymbol Symbol { get; } = symbol;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
    }
}

public class SemanticFieldReferenceNode(
    Token identifier,
    FieldSymbol symbol,
    SemanticNode? explicitObjectInstance,
    bool isStatic,
    IDataType dataType
)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public FieldSymbol Symbol { get; } = symbol;

    public SemanticNode? ExplicitObjectInstance { get; } = explicitObjectInstance;

    public bool IsStatic { get; } = isStatic;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
    }
}

public class SemanticUnaryNode(TokenKind op, SemanticNode value, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public TokenKind Operator { get; } = op;

    public SemanticNode Value { get; } = value;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Value.Traverse(callback);
        callback(Value, this);
    }
}

public class SemanticBinaryNode(SemanticNode left, TokenKind op, SemanticNode right, IDataType dataType)
    : SemanticNode(dataType, left.Span.Combine(right.Span))
{
    public SemanticNode Left { get; } = left;

    public TokenKind Operator { get; } = op;

    public SemanticNode Right { get; } = right;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Left.Traverse(callback);
        Right.Traverse(callback);
        callback(Left, this);
        callback(Right, this);
    }
}

public class SemanticAssignmentNode(ISymbol leftSymbol, SemanticNode right, TextSpan span)
    : SemanticNode(right.DataType, span)
{
    public ISymbol Left { get; } = leftSymbol;

    public SemanticNode Right { get; } = right;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Right.Traverse(callback);
        callback(Right, this);
    }
}

public class SemanticCallNode(SemanticNode left, List<SemanticNode> arguments, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public SemanticNode Left { get; } = left;

    public List<SemanticNode> Arguments { get; } = arguments;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Left.Traverse(callback);
        callback(Left, this);
        foreach (var argument in Arguments)
        {
            argument.Traverse(callback);
            callback(argument, this);
        }
    }
}

public class SemanticNewNode(List<SemanticNode> arguments, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public List<SemanticNode> Arguments { get; } = arguments;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var argument in Arguments)
        {
            argument.Traverse(callback);
            callback(argument, this);
        }
    }
}

public class SemanticReturnNode(SemanticNode? value, TextSpan span)
    : SemanticNode(value?.DataType ?? new PrimitiveDataType(Primitive.Void), span)
{
    public SemanticNode? Value { get; } = value;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        if (Value != null)
        {
            Value.Traverse(callback);
            callback(Value, this);
        }
    }
}

public class SemanticKeywordValueNode(Token keyword, List<SemanticNode> arguments, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public Token Keyword { get; } = keyword;

    public List<SemanticNode> Arguments { get; } = arguments;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var argument in Arguments)
        {
            argument.Traverse(callback);
            callback(argument, this);
        }
    }
}

public class SemanticBlockNode(List<SemanticNode> expressions, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public List<SemanticNode> Expressions { get; } = expressions;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var expression in Expressions)
        {
            expression.Traverse(callback);
            callback(expression, this);
        }
    }
}

public class SemanticAttributeNode(Token identifier, List<SemanticNode> arguments, TextSpan span)
    : SemanticNode(new PrimitiveDataType(Primitive.Void), span)
{
    public Token Identifier { get; } = identifier;

    public List<SemanticNode> Arguments { get; } = arguments;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var argument in Arguments)
        {
            argument.Traverse(callback);
            callback(argument, this);
        }
    }
}

public class SemanticTypeNode(IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
    }
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

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Value.Traverse(callback);
        callback(Value, this);
    }
}

public class SemanticFunctionDeclarationNode(
    Token identifier,
    List<SemanticParameterNode> parameters,
    IDataType returnType,
    SemanticBlockNode? body,
    bool isStatic,
    FunctionSymbol symbol,
    TextSpan span
)
    : SemanticNode(new PrimitiveDataType(Primitive.Void), span)
{
    public Token Identifier { get; } = identifier;

    public List<SemanticParameterNode> Parameters { get; } = parameters;

    public IDataType ReturnType { get; } = returnType;

    public SemanticBlockNode? Body { get; } = body;

    public bool IsStatic { get; } = isStatic;

    public FunctionSymbol Symbol { get; } = symbol;

    public List<SemanticAttributeNode> Attributes { get; init; } = [];

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var parameter in Parameters)
        {
            parameter.Traverse(callback);
            callback(parameter, this);
        }

        if (Body != null)
        {
            Body.Traverse(callback);
            callback(Body, this);
        }
    }
}

public class SemanticParameterNode(Token identifier, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span), ISemanticVariableDeclaration
{
    public Token Identifier { get; } = identifier;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
    }
}

public interface ISemanticStructureDeclaration
{
    Token Identifier { get; }

    IDataType DataType { get; }

    StructureSymbol Symbol { get; }

    List<SemanticFunctionDeclarationNode> Functions { get; }

    List<SemanticFieldDeclarationNode> Fields { get; }

    int FieldStartIndex { get; }
}

public interface ISemanticInstantiableStructureDeclaration : ISemanticStructureDeclaration
{
    public SemanticInitNode Init { get; }
}

public class SemanticClassDeclarationNode(
    Token identifier,
    SemanticInitNode init,
    List<SemanticFunctionDeclarationNode> functions,
    List<SemanticFieldDeclarationNode> fields,
    StructureSymbol symbol,
    TextSpan span
)
    : SemanticNode(new PrimitiveDataType(Primitive.Void), span), ISemanticStructureDeclaration, ISemanticInstantiableStructureDeclaration
{
    public Token Identifier { get; } = identifier;

    public SemanticInitNode Init { get; } = init;

    public List<SemanticFunctionDeclarationNode> Functions { get; } = functions;

    public List<SemanticFieldDeclarationNode> Fields { get; } = fields;

    public StructureSymbol Symbol { get; } = symbol;

    public int FieldStartIndex { get; } = 0;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var field in Fields)
        {
            field.Traverse(callback);
            callback(field, this);
        }

        foreach (var function in Functions)
        {
            function.Traverse(callback);
            callback(function, this);
        }
    }
}

public class SemanticFieldDeclarationNode(
    Token identifier,
    SemanticNode? value,
    bool isStatic,
    IDataType dataType,
    FieldSymbol symbol,
    TextSpan span
)
    : SemanticNode(dataType, span)
{
    public Token Identifier { get; } = identifier;

    public SemanticNode? Value { get; } = value;

    public bool IsStatic { get; } = isStatic;

    public FieldSymbol Symbol { get; } = symbol;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        if (Value != null)
        {
            Value.Traverse(callback);
            callback(Value, this);
        }
    }
}

public class SemanticInitNode(List<SemanticParameterNode> parameters, SemanticBlockNode body, TextSpan span)
    : SemanticNode(new PrimitiveDataType(Primitive.Void), span)
{
    public List<SemanticParameterNode> Parameters { get; } = parameters;

    public SemanticBlockNode Body { get; } = body;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var parameter in Parameters)
        {
            parameter.Traverse(callback);
            callback(parameter, this);
        }

        Body.Traverse(callback);
        callback(Body, this);
    }
}

public class SemanticInitParameterNode(Token identifier, IDataType dataType, TextSpan span)
    : SemanticNode(dataType, span)
{
    public Token Identifier { get; } = identifier;

    public StructureSymbol? ResolvedSymbol { get; set; }

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
    }
}
