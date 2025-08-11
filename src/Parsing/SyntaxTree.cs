using System.Security.Principal;
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

    public static SyntaxBlockNode? GetEnclosingBlock(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not (null or SyntaxBlockNode))
            current = current.Parent;

        return current as SyntaxBlockNode;
    }

    public static SyntaxFunctionDeclarationNode? GetEnclosingFunction(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not (null or SyntaxFunctionDeclarationNode))
            current = current.Parent;

        return current as SyntaxFunctionDeclarationNode;
    }

    public static ISyntaxStructureDeclaration? GetEnclosingStructure(SyntaxNode node)
    {
        var current = node;
        while (current is not (null or ISyntaxStructureDeclaration))
            current = current.Parent;

        return current as ISyntaxStructureDeclaration;
    }

    public static SyntaxFieldDeclarationNode? GetEnclosingField(SyntaxNode node)
    {
        var current = node;
        while (current is not (null or SyntaxFieldDeclarationNode))
            current = current.Parent;

        return current as SyntaxFieldDeclarationNode;
    }

    public static SyntaxBlockNode? GetEnclosingGetter(SyntaxNode node)
    {
        var field = GetEnclosingField(node);
        if (field == null)
            return null;

        var currentBlock = GetEnclosingBlock(node);
        while (currentBlock != null && currentBlock != field.Getter)
            currentBlock = GetEnclosingBlock(currentBlock);

        if (currentBlock == field.Getter)
            return field.Getter;

        return null;
    }

    public static SyntaxBlockNode? GetEnclosingSetter(SyntaxNode node)
    {
        var field = GetEnclosingField(node);
        if (field == null)
            return null;

        var currentBlock = GetEnclosingBlock(node);
        while (currentBlock != null && currentBlock != field.Setter)
            currentBlock = GetEnclosingBlock(currentBlock);

        if (currentBlock == field.Setter)
            return field.Setter;

        return null;
    }

    public static FileScope? GetFileScope(SyntaxNode node)
    {
        return node.Span.Start.SyntaxTree.File;
    }

    public static StructureScope? GetStructureScope(SyntaxNode node)
    {
        return GetEnclosingStructure(node)?.Scope;
    }

    public static LocalScope? GetLocalScope(SyntaxNode node)
    {
        var current = node;
        while (current is not (null or SyntaxBlockNode))
            current = current.Parent;

        return (current as SyntaxBlockNode)?.Scope as LocalScope;
    }

    public static TypeScope? GetTypeScope(SyntaxNode node)
    {
        return GetEnclosingFunction(node)?.TypeScope
            ?? GetEnclosingStructure(node)?.TypeScope;
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

public class SyntaxWithNode(List<Token> identifiers, TextSpan span)
    : SyntaxNode(span)
{
    public List<Token> Identifiers { get; } = identifiers;
}

public class SyntaxStatementNode(SyntaxNode expression, bool hasTrailingSemicolon)
    : SyntaxNode(expression.Span)
{
    public SyntaxNode Expression { get; } = expression;

    public bool HasTrailingSemicolon { get; } = hasTrailingSemicolon;
}

public class SyntaxGroupNode(SyntaxNode value, TextSpan span)
    : SyntaxNode(span)
{
    public SyntaxNode Value { get; } = value;
}

public class SyntaxLiteralNode(Token value)
    : SyntaxNode(value.Span)
{
    public Token Value { get; } = value;
}

public class SyntaxIdentifierNode(
    List<Token> identifierList,
    List<SyntaxTypeNode> typeArguments,
    TextSpan span
)
    : SyntaxNode(span)
{
    public List<Token> IdentifierList { get; } = identifierList;

    public List<SyntaxTypeNode> TypeArguments { get; } = typeArguments;
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

public class SyntaxMemberAccessNode(SyntaxNode left, SyntaxIdentifierNode identifierNode)
    : SyntaxNode(left.Span.Combine(identifierNode.Span))
{
    public SyntaxNode Left { get; } = left;

    public SyntaxIdentifierNode IdentifierNode { get; } = identifierNode;
}

public class SyntaxCallNode(
    SyntaxNode left,
    List<SyntaxNode> arguments,
    TextSpan span
    )
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

public class SyntaxReturnNode(SyntaxNode? value, TextSpan span)
    : SyntaxNode(span)
{
    public SyntaxNode? Value { get; } = value;
}

public class SyntaxKeywordValueNode(Token keyword, List<SyntaxNode>? arguments, TextSpan span)
    : SyntaxNode(span)
{
    public Token Keyword { get; } = keyword;

    public List<SyntaxNode>? Arguments { get; } = arguments;
}

public class SyntaxDotKeywordNode(SyntaxNode left, Token keyword, List<SyntaxNode>? arguments, TextSpan span)
    : SyntaxNode(span)
{
    public SyntaxNode Left { get; } = left;

    public Token Keyword { get; } = keyword;

    public List<SyntaxNode>? Arguments { get; } = arguments;
}

public class SyntaxIfNode(SyntaxNode condition, SyntaxBlockNode thenBranch, SyntaxBlockNode? elseBranch, TextSpan span)
    : SyntaxNode(span)
{
    public SyntaxNode Condition { get; } = condition;

    public SyntaxBlockNode ThenBranch { get; } = thenBranch;

    public SyntaxBlockNode? ElseBranch { get; } = elseBranch;
}

public class SyntaxBlockNode(List<SyntaxNode> expressions, bool isArrowBlock, TextSpan span)
    : SyntaxNode(span)
{
    public List<SyntaxNode> Expressions { get; } = expressions;

    public bool IsArrowBlock { get; } = isArrowBlock;

    public IScope? Scope { get; set; }
}

public class SyntaxParameterNode(Token identifier, SyntaxTypeNode type, TextSpan span)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public SyntaxTypeNode Type { get; } = type;
}

public class SyntaxTypeNode(
    List<Token> typeNames,
    bool isSlice,
    List<SyntaxTypeNode> typeArguments,
    TextSpan span
)
    : SyntaxNode(span)
{
    public List<Token> TypeNames { get; } = typeNames;

    public bool IsSlice { get; } = isSlice;

    public List<SyntaxTypeNode> TypeArguments { get; } = typeArguments;

    public ISymbol? ResolvedSymbol { get; set; }
}

public class SyntaxAttributeNode(Token identifier, List<SyntaxNode> arguments, TextSpan span)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public List<SyntaxNode> Arguments { get; } = arguments;
}

public class SyntaxVariableDeclarationNode(bool isMutable, Token identifier, SyntaxTypeNode? type, SyntaxNode value, TextSpan span)
    : SyntaxNode(span)
{
    public bool IsMutable { get; } = isMutable;

    public Token Identifier { get; } = identifier;

    public SyntaxTypeNode? Type { get; } = type;

    public SyntaxNode Value { get; } = value;
}

public interface ISyntaxFunctionDeclaration
{
    Token Identifier { get; }

    List<SyntaxParameterNode> Parameters { get; }

    SyntaxTypeNode? ReturnType { get; }

    FunctionSymbol? Symbol { get; }
}

public class SyntaxFunctionDeclarationNode(
    Token identifier,
    List<SyntaxTypeParameterNode> typeParameters,
    List<SyntaxParameterNode> parameters,
    SyntaxTypeNode? returnType,
    SyntaxBlockNode? body,
    bool isPublic,
    bool isStatic,
    bool isOverride,
    TypeScope typeScope,
    TextSpan span
)
    : SyntaxNode(span), ISyntaxFunctionDeclaration
{
    public Token Identifier { get; } = identifier;

    public List<SyntaxTypeParameterNode> TypeParameters { get; } = typeParameters;

    public List<SyntaxParameterNode> Parameters { get; } = parameters;

    public SyntaxTypeNode? ReturnType { get; } = returnType;

    public SyntaxBlockNode? Body { get; } = body;

    public bool IsPublic { get; } = isPublic;

    public bool IsStatic { get; } = isStatic;

    public bool IsOverride { get; } = isOverride;

    public FunctionSymbol? Symbol { get; set; }

    public TypeScope TypeScope { get; } = typeScope;

    public List<SyntaxAttributeNode> Attributes { get; init; } = [];
}

public interface ISyntaxStructureDeclaration
{
    Token Identifier { get; }

    StructureScope Scope { get; }

    TypeScope TypeScope { get; }

    StructureSymbol? Symbol { get; }

    List<SyntaxTypeNode> SubTypes { get; }

    List<SyntaxTypeParameterNode> TypeParameters { get; }

    ISymbol? ResolveSymbol(string name)
    {
        var localSymbol = Scope.FindSymbol(name);
        if (localSymbol != null)
            return localSymbol;

        foreach (var subType in SubTypes)
        {
            if (subType.ResolvedSymbol is not StructureSymbol structureSymbol)
                continue;

            var resolved = structureSymbol.SyntaxDeclaration.ResolveSymbol(name);
            if (resolved != null)
                return resolved;
        }

        return null;
    }
}

public interface ISyntaxInstantiableStructureDeclaration : ISyntaxStructureDeclaration
{
    SyntaxInitNode? Init { get; }
}

public interface ISyntaxReferenceTypeDeclaration : ISyntaxStructureDeclaration
{
}

public class SyntaxTypeParameterNode(Token identifier, TypeSymbol symbol)
    : SyntaxNode(identifier.Span)
{
    public Token Identifier { get; } = identifier;

    public TypeSymbol Symbol { get; } = symbol;
}

public class SyntaxClassDeclarationNode(
    Token identifier,
    List<SyntaxTypeNode> subTypes,
    List<SyntaxTypeParameterNode> typeParameters,
    SyntaxInitNode? constructor,
    List<SyntaxNode> declarations,
    bool isInheritable,
    StructureScope scope,
    TypeScope typeScope,
    TextSpan span
)
    : SyntaxNode(span), ISyntaxStructureDeclaration, ISyntaxInstantiableStructureDeclaration, ISyntaxReferenceTypeDeclaration
{
    public Token Identifier { get; } = identifier;

    public List<SyntaxTypeNode> SubTypes { get; } = subTypes;

    public List<SyntaxTypeParameterNode> TypeParameters { get; } = typeParameters;

    public SyntaxInitNode? Init { get; } = constructor;

    public List<SyntaxNode> Declarations { get; } = declarations;

    public bool IsInheritable { get; } = isInheritable;

    public StructureScope Scope { get; } = scope;

    public TypeScope TypeScope { get; } = typeScope;

    public StructureSymbol? Symbol { get; set; }
}

public class SyntaxProtocolDeclarationNode(
    Token identifier,
    List<SyntaxTypeNode> subTypes,
    List<SyntaxNode> declarations,
    StructureScope scope,
    TextSpan span
)
    : SyntaxNode(span), ISyntaxStructureDeclaration
{
    public Token Identifier { get; } = identifier;

    public List<SyntaxTypeNode> SubTypes { get; } = subTypes;

    public List<SyntaxTypeParameterNode> TypeParameters { get; } = [];

    public List<SyntaxNode> Declarations { get; } = declarations;

    public StructureScope Scope { get; } = scope;

    public TypeScope TypeScope { get; } = new(parent: null);

    public StructureSymbol? Symbol { get; set; }
}

public class SyntaxModuleDeclarationNode(
    Token identifier,
    List<SyntaxNode> declarations,
    StructureScope scope,
    TextSpan span
)
    : SyntaxNode(span), ISyntaxStructureDeclaration
{
    public Token Identifier { get; } = identifier;

    public List<SyntaxTypeNode> SubTypes { get; } = [];

    public List<SyntaxTypeParameterNode> TypeParameters { get; } = [];

    public List<SyntaxNode> Declarations { get; } = declarations;

    public StructureScope Scope { get; } = scope;

    public TypeScope TypeScope { get; } = new(parent: null);

    public StructureSymbol? Symbol { get; set; }
}

public class SyntaxEnumDeclarationNode(
    Token identifier,
    List<SyntaxEnumMemberNode> members,
    SyntaxTypeNode? type,
    TextSpan span
)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public List<SyntaxEnumMemberNode> Members { get; } = members;

    public SyntaxTypeNode? Type { get; } = type;

    public EnumSymbol? Symbol { get; set; }
}

public class SyntaxEnumMemberNode(Token identifier, SyntaxNode? value, TextSpan span)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public SyntaxNode? Value { get; } = value;
}

public class SyntaxFieldDeclarationNode(
    Token identifier,
    SyntaxTypeNode type,
    SyntaxNode? value,
    bool isMutable,
    bool isPublic,
    bool isStatic,
    SyntaxBlockNode? getter,
    SyntaxBlockNode? setter,
    TextSpan span
)
    : SyntaxNode(span)
{
    public Token Identifier { get; } = identifier;

    public SyntaxTypeNode Type { get; } = type;

    public SyntaxNode? Value { get; } = value;

    public bool IsMutable { get; } = isMutable;

    public bool IsPublic { get; } = isPublic;

    public bool IsStatic { get; } = isStatic;

    public SyntaxBlockNode? Getter { get; } = getter;

    public SyntaxBlockNode? Setter { get; } = setter;

    public FieldSymbol? Symbol { get; set; }

    public List<SyntaxAttributeNode> Attributes { get; set; } = [];
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
