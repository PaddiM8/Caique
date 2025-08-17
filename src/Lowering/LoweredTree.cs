using Caique.Analysis;
using Caique.Lexing;
using Caique.Scope;

namespace Caique.Lowering;

public class LoweredTree(
    string moduleName,
    FileScope fileScope,
    Dictionary<string, LoweredFunctionDeclarationNode> functions,
    Dictionary<string, LoweredStructDeclarationNode> structs,
    Dictionary<string, LoweredGlobalDeclarationNode> globals
)
{
    public string ModuleName { get; } = moduleName;

    public FileScope FileScope { get; } = fileScope;

    public Dictionary<string, LoweredFunctionDeclarationNode> Functions { get; } = functions;

    public Dictionary<string, LoweredStructDeclarationNode> Structs { get; } = structs;

    public Dictionary<string, LoweredGlobalDeclarationNode> Globals { get; } = globals;
}

public abstract class LoweredNode(ILoweredDataType dataType)
{
    public ILoweredDataType DataType { get; } = dataType;
}

public class LoweredStatementStartNode(TextSpan span)
    : LoweredNode(new LoweredPrimitiveDataType(Primitive.Void))
{
    public TextSpan Span { get; } = span;
}

public class LoweredLoadNode(LoweredNode value)
    : LoweredNode(value.DataType.Dereference())
{
    public LoweredNode Value { get; } = value;
}

public class LoweredLiteralNode(string value, TokenKind kind, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public string Value { get; } = value;

    public TokenKind Kind { get; } = kind;
}

public class LoweredVariableReferenceNode(ILoweredVariableDeclaration declaration, ILoweredDataType dataType)
    : LoweredNode(new LoweredPointerDataType(dataType))
{
    public ILoweredVariableDeclaration Declaration { get; } = declaration;
}

public class LoweredFunctionReferenceNode(string name, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public string Identifier { get; } = name;
}

public class LoweredFieldReferenceNode(
    LoweredStructDataType instanceDataType,
    LoweredNode instance,
    int index,
    ILoweredDataType dataType
)
    : LoweredNode(new LoweredPointerDataType(dataType))
{
    public LoweredStructDataType InstanceDataType { get; } = instanceDataType;

    public LoweredNode Instance { get; } = instance;

    public int Index { get; } = index;
}

public class LoweredFieldReferenceByNameNode(
    LoweredStructDataType instanceDataType,
    LoweredNode instance,
    string name,
    ILoweredDataType dataType
)
    : LoweredNode(new LoweredPointerDataType(dataType))
{
    public LoweredStructDataType InstanceDataType { get; } = instanceDataType;

    public LoweredNode Instance { get; } = instance;

    public string Name { get; } = name;
}

public class LoweredOnDemandReferencePlaceholderNode()
    : LoweredNode(new LoweredPrimitiveDataType(Primitive.Void))
{
}

public class LoweredGlobalReferenceNode(string identifier, ILoweredDataType dataType)
    : LoweredNode(new LoweredPointerDataType(dataType))
{
    public string Identifier { get; } = identifier;
}

public class LoweredUnaryNode(TokenKind op, LoweredNode value, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public TokenKind Operator { get; } = op;

    public LoweredNode Value { get; } = value;
}

public class LoweredBinaryNode(LoweredNode left, TokenKind op, LoweredNode right, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public LoweredNode Left { get; } = left;

    public TokenKind Operator { get; } = op;

    public LoweredNode Right { get; } = right;
}

public class LoweredAssignmentNode(LoweredNode assignee, LoweredNode value)
    : LoweredNode(value.DataType)
{
    public LoweredNode Assignee { get; } = assignee;

    public LoweredNode Value { get; } = value;
}

public class LoweredCallNode(LoweredNode callee, List<LoweredNode> arguments, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public LoweredNode Callee { get; } = callee;

    public List<LoweredNode> Arguments { get; } = arguments;
}

public class LoweredConstStructNode(List<LoweredNode> values, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public List<LoweredNode> Values { get; } = values;
}

public class LoweredReturnNode(LoweredNode? value)
    : LoweredNode(value?.DataType ?? new LoweredPrimitiveDataType(Primitive.Void))
{
    public LoweredNode? Value { get; } = value;
}

public enum KeywordValueKind
{
    Self,
    Base,
    Default,
}

public class LoweredKeywordValueNode(KeywordValueKind kind, List<LoweredNode> arguments, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public KeywordValueKind Kind { get; } = kind;

    public List<LoweredNode> Arguments { get; } = arguments;
}

public class LoweredSizeOfNode(ILoweredDataType argument)
    : LoweredNode(new LoweredPrimitiveDataType(Primitive.USize))
{
    public ILoweredDataType Argument { get; } = argument;
}

public class LoweredIfNode(
    LoweredNode condition,
    LoweredBlockNode thenBranch,
    LoweredBlockNode? elseBranch,
    ILoweredDataType dataType
)
    : LoweredNode(dataType)
{
    public LoweredNode Condition { get; } = condition;

    public LoweredBlockNode ThenBranch { get; } = thenBranch;

    public LoweredBlockNode? ElseBranch { get; } = elseBranch;
}

public class LoweredCastNode(LoweredNode value, ILoweredDataType dataType)
    : LoweredNode(dataType)
{
    public LoweredNode Value { get; } = value;
}

public class LoweredBlockNode(
    List<LoweredNode> expressions,
    LoweredVariableDeclarationNode? returnValueDeclaration,
    ILoweredDataType dataType
)
    : LoweredNode(dataType)
{
    public List<LoweredNode> Expressions { get; } = expressions;

    public LoweredVariableDeclarationNode? ReturnValueDeclaration { get; } = returnValueDeclaration;
}

public class LoweredTypeNode(ILoweredDataType dataType)
    : LoweredNode(dataType)
{
}

public interface ILoweredVariableDeclaration
{
    string Identifier { get; }

    LoweredNode? Value { get; }

    ILoweredDataType DataType { get; }
}

public class LoweredVariableDeclarationNode(string identifier, LoweredNode? value, ILoweredDataType dataType)
    : LoweredNode(dataType), ILoweredVariableDeclaration
{
    public string Identifier { get; } = identifier;

    public LoweredNode? Value { get; } = value;
}

public class LoweredFunctionDeclarationNode(
    string identifier,
    List<LoweredParameterNode> parameters,
    LoweredBlockNode? body,
    ILoweredDataType dataType,
    TextSpan span
)
    : LoweredNode(dataType)
{
    public string Identifier { get; } = identifier;

    public List<LoweredParameterNode> Parameters { get; } = parameters;

    public LoweredBlockNode? Body { get; set; } = body;

    public TextSpan Span { get; } = span;
}

public class LoweredParameterNode(string identifier, ILoweredDataType dataType)
    : LoweredNode(dataType), ILoweredVariableDeclaration
{
    public string Identifier { get; } = identifier;

    public LoweredNode? Value { get; } = null;
}

public class LoweredStructDeclarationNode(
    string identifier,
    List<LoweredFieldDeclarationNode> fields,
    StructureSymbol symbol
)
    : LoweredNode(new LoweredPrimitiveDataType(Primitive.Void))
{
    public string Identifier { get; } = identifier;

    public List<LoweredFieldDeclarationNode> Fields { get; } = fields;

    public StructureSymbol Symbol { get; } = symbol;
}

public class LoweredFieldDeclarationNode(
    string identifier,
    LoweredNode value,
    ILoweredDataType dataType
)
    : LoweredNode(dataType)
{
    public string Identifier { get; } = identifier;

    public LoweredNode Value { get; } = value;
}

public enum LoweredGlobalScope
{
    Module,
    Full,
}

public class LoweredGlobalDeclarationNode(
    string identifier,
    LoweredNode? value,
    LoweredGlobalScope scope,
    ILoweredDataType dataType
)
    : LoweredNode(dataType)
{
    public string Identifier { get; } = identifier;

    public LoweredNode? Value { get; } = value;

    public LoweredGlobalScope Scope { get; } = scope;
}

