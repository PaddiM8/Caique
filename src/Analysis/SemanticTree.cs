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

public class SemanticStatementNode(SemanticNode value)
    : SemanticNode(value.DataType, value.Span)
{
    public SemanticNode Value { get; } = value;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Value.Traverse(callback);
        callback(Value, this);
    }
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

public class SemanticFunctionReferenceNode(
    Token identifier,
    FunctionSymbol symbol,
    SemanticNode? objectInstance,
    IDataType dataType
)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public FunctionSymbol Symbol { get; } = symbol;

    public SemanticNode? ObjectInstance { get; } = objectInstance;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        if (ObjectInstance != null)
        {
            ObjectInstance.Traverse(callback);
            callback(ObjectInstance, this);
        }
    }
}

public class SemanticFieldReferenceNode(
    Token identifier,
    FieldSymbol symbol,
    SemanticNode? objectInstance,
    IDataType dataType
)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public FieldSymbol Symbol { get; } = symbol;

    public SemanticNode? ObjectInstance { get; } = objectInstance;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        if (ObjectInstance != null)
        {
            ObjectInstance.Traverse(callback);
            callback(ObjectInstance, this);
        }
    }
}

public class SemanticEnumReferenceNode(Token identifier, EnumSymbol symbol, IDataType dataType)
    : SemanticNode(dataType, identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public EnumSymbol Symbol { get; } = symbol;

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

public class SemanticAssignmentNode(SemanticNode left, SemanticNode right, TextSpan span)
    : SemanticNode(right.DataType, span)
{
    public SemanticNode Left { get; } = left;

    public SemanticNode Right { get; } = right;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Left.Traverse(callback);
        callback(Left, this);

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
    : SemanticNode(value?.DataType ?? PrimitiveDataType.Void, span)
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

public class SemanticKeywordValueNode(Token keyword, List<SemanticNode>? arguments, TextSpan span, IDataType dataType)
    : SemanticNode(dataType, span)
{
    public Token Keyword { get; } = keyword;

    public List<SemanticNode>? Arguments { get; } = arguments;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        if (Arguments == null)
            return;

        foreach (var argument in Arguments)
        {
            argument.Traverse(callback);
            callback(argument, this);
        }
    }
}

public class SemanticCastNode(
    SemanticNode value,
    TextSpan span,
    IDataType dataType
)
    : SemanticNode(dataType, span)
{
    public SemanticNode Value { get; } = value;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        Value.Traverse(callback);
        callback(Value, this);
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
    : SemanticNode(PrimitiveDataType.Void, span)
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

public interface ISemanticFunctionDeclaration
{
    Token Identifier { get; }

    List<SemanticParameterNode> Parameters { get; }

    IDataType ReturnType { get; }

    FunctionSymbol Symbol { get; }

    TextSpan Span { get; }
}

public class SemanticFunctionDeclarationNode(
    Token identifier,
    List<SemanticParameterNode> parameters,
    IDataType returnType,
    SemanticBlockNode? body,
    bool isStatic,
    bool isOverride,
    FunctionSymbol symbol,
    TextSpan span
)
    : SemanticNode(PrimitiveDataType.Void, span), ISemanticFunctionDeclaration
{
    public Token Identifier { get; } = identifier;

    public List<SemanticParameterNode> Parameters { get; } = parameters;

    public IDataType ReturnType { get; } = returnType;

    public SemanticBlockNode? Body { get; } = body;

    public bool IsStatic { get; } = isStatic;

    public bool IsOverride { get; } = isOverride;

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

        foreach (var attribute in Attributes)
        {
            attribute.Traverse(callback);
            callback(attribute, this);
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
    StructureSymbol? inheritedClass,
    List<StructureSymbol> implementedProtocols,
    SemanticInitNode init,
    List<SemanticFunctionDeclarationNode> functions,
    List<SemanticFieldDeclarationNode> fields,
    bool isInheritable,
    StructureSymbol symbol,
    TextSpan span
)
    : SemanticNode(PrimitiveDataType.Void, span), ISemanticStructureDeclaration, ISemanticInstantiableStructureDeclaration
{
    public Token Identifier { get; } = identifier;

    public StructureSymbol? InheritedClass { get; } = inheritedClass;

    public List<StructureSymbol> ImplementedProtocols { get; } = implementedProtocols;

    public SemanticInitNode Init { get; } = init;

    public List<SemanticFunctionDeclarationNode> Functions { get; } = functions;

    public List<SemanticFieldDeclarationNode> Fields { get; } = fields;

    public bool IsInheritable { get; } = isInheritable;

    public StructureSymbol Symbol { get; } = symbol;

    public int FieldStartIndex { get; init; }

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var field in Fields)
        {
            field.Traverse(callback);
            callback(field, this);
        }

        Init.Traverse(callback);
        callback(Init, this);

        foreach (var function in Functions)
        {
            function.Traverse(callback);
            callback(function, this);
        }
    }

    public IEnumerable<SemanticFieldDeclarationNode> GetAllMemberFields()
    {
        var inheritedFields = InheritedClass == null
            ? []
            : ((SemanticClassDeclarationNode)InheritedClass.SemanticDeclaration!).GetAllMemberFields();
        var memberFields = Fields.Where(x => !x.IsStatic);

        return inheritedFields.Concat(memberFields);
    }

    public IEnumerable<SemanticFunctionDeclarationNode> GetAllMethods()
    {
        var inheritedFunctions = InheritedClass == null
            ? []
            : ((SemanticClassDeclarationNode)InheritedClass.SemanticDeclaration!).GetAllMethods();
        var memberFunctions = Functions.Where(x => !x.IsStatic);

        return inheritedFunctions.Concat(memberFunctions);
    }
}

public class SemanticProtocolDeclarationNode(
    Token identifier,
    List<SemanticFunctionDeclarationNode> functions,
    StructureSymbol symbol,
    TextSpan span
)
    : SemanticNode(PrimitiveDataType.Void, span), ISemanticStructureDeclaration
{
    public Token Identifier { get; } = identifier;

    public List<SemanticFunctionDeclarationNode> Functions { get; } = functions;

    public List<SemanticFieldDeclarationNode> Fields { get; } = [];

    public StructureSymbol Symbol { get; } = symbol;

    public int FieldStartIndex { get; }

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var function in Functions)
        {
            function.Traverse(callback);
            callback(function, this);
        }
    }
}

public class SemanticModuleDeclarationNode(
    Token identifier,
    List<SemanticFunctionDeclarationNode> functions,
    List<SemanticFieldDeclarationNode> fields,
    StructureSymbol symbol,
    TextSpan span
)
    : SemanticNode(PrimitiveDataType.Void, span), ISemanticStructureDeclaration
{
    public Token Identifier { get; } = identifier;

    public List<SemanticFunctionDeclarationNode> Functions { get; } = functions;

    public List<SemanticFieldDeclarationNode> Fields { get; } = fields;

    public StructureSymbol Symbol { get; } = symbol;

    public int FieldStartIndex { get; }

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var function in Functions)
        {
            function.Traverse(callback);
            callback(function, this);
        }

        foreach (var field in Fields)
        {
            field.Traverse(callback);
            callback(field, this);
        }
    }
}

public class SemanticEnumDeclarationNode(
    Token identifier,
    List<SemanticEnumMemberNode> members,
    IDataType memberDataType,
    EnumSymbol symbol,
    TextSpan span
)
    : SemanticNode(new EnumDataType(symbol), span)
{
    public Token Identifier { get; } = identifier;

    public List<SemanticEnumMemberNode> Members { get; } = members;

    public IDataType MemberDataType { get; } = memberDataType;

    public EnumSymbol Symbol { get; } = symbol;

    public int FieldStartIndex { get; }

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var member in Members)
        {
            member.Traverse(callback);
            callback(member, this);
        }
    }
}

public class SemanticEnumMemberNode(
    Token identifier,
    SemanticLiteralNode value,
    IDataType dataType,
    TextSpan span
)
    : SemanticNode(dataType, span)
{
    public Token Identifier { get; } = identifier;

    public SemanticLiteralNode Value { get; } = value;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        if (Value != null)
        {
            Value.Traverse(callback);
            callback(Value, this);
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

    public List<SemanticAttributeNode> Attributes { get; init; } = [];

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        if (Value != null)
        {
            Value.Traverse(callback);
            callback(Value, this);
        }

        foreach (var attribute in Attributes)
        {
            attribute.Traverse(callback);
            callback(attribute, this);
        }
    }
}

public class SemanticInitNode(
    List<SemanticParameterNode> parameters,
    SemanticKeywordValueNode? baseCall,
    SemanticBlockNode body,
    TextSpan span
)
    : SemanticNode(PrimitiveDataType.Void, span)
{
    public List<SemanticParameterNode> Parameters { get; } = parameters;

    public SemanticKeywordValueNode? BaseCall { get; } = baseCall;

    public SemanticBlockNode Body { get; } = body;

    public override void Traverse(Action<SemanticNode, SemanticNode> callback)
    {
        foreach (var parameter in Parameters)
        {
            parameter.Traverse(callback);
            callback(parameter, this);
        }

        if (BaseCall != null)
        {
            BaseCall.Traverse(callback);
            callback(BaseCall, this);
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
