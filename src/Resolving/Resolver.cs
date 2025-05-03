using System.Diagnostics;
using Caique.Analysis;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;

namespace Caique.Resolving;

/// <summary>
/// Resolves top-level types and node parents.
/// </summary>
public class Resolver
{
    private readonly SyntaxTree _syntaxTree;
    private readonly DiagnosticReporter _diagnostics;

    private Resolver(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        _syntaxTree = syntaxTree;
        _diagnostics = compilationContext.DiagnosticReporter;
    }

    public static void Resolve(SyntaxTree syntaxTree, CompilationContext compilationContext)
    {
        var resolver = new Resolver(syntaxTree, compilationContext);
        Debug.Assert(syntaxTree.Root != null);
        resolver.Next(syntaxTree.Root, null);
    }

    private void Next(SyntaxNode node, SyntaxNode? parent)
    {
        node.Parent = parent;

        switch (node)
        {
            case SyntaxStatementNode statementNode:
                Next(statementNode.Expression, node);
                break;
            case SyntaxUnaryNode unaryNode:
                Next(unaryNode.Value, node);
                break;
            case SyntaxBinaryNode binaryNode:
                Next(binaryNode.Left, node);
                Next(binaryNode.Right, node);
                break;
            case SyntaxAssignmentNode assignmentNode:
                Next(assignmentNode.Left, node);
                Next(assignmentNode.Right, node);
                break;
            case SyntaxCallNode callNode:
                Visit(callNode);
                break;
            case SyntaxNewNode newNode:
                Visit(newNode);
                break;
            case SyntaxReturnNode returnNode:
                if (returnNode.Value != null)
                    Next(returnNode.Value, node);
                break;
            case SyntaxBlockNode blockNode:
                Visit(blockNode);
                break;
            case SyntaxVariableDeclarationNode variableDeclarationNode:
                Next(variableDeclarationNode.Value, node);
                break;
            case SyntaxFunctionDeclarationNode functionDeclarationNode:
                Visit(functionDeclarationNode);
                break;
            case SyntaxParameterNode parameterNode:
                Next(parameterNode.Type, node);
                ResolveNode(parameterNode.Type);
                break;
            case SyntaxClassDeclarationNode classDeclarationNode:
                Visit(classDeclarationNode);
                break;
            case SyntaxFieldDeclarationNode fieldDeclarationNode:
                Visit(fieldDeclarationNode);
                break;
            default:
                break;
        }
    }

    private void ResolveNode(SyntaxTypeNode typeNode)
    {
        // Primitives don't need to be resolved
        if (typeNode.TypeNames.SingleOrDefault()?.Kind != TokenKind.Identifier)
            return;

        var typeNames = typeNode.TypeNames.Select(x => x.Value).ToList();
        var resolvedType = _syntaxTree.Namespace.ResolveStructure(typeNames);
        typeNode.ResolvedSymbol = resolvedType;
    }

    private void Visit(SyntaxCallNode node)
    {
        Next(node.Left, node);
        foreach (var argument in node.Arguments)
            Next(argument, node);
    }

    private void Visit(SyntaxNewNode node)
    {
        Next(node.Type, node);
        foreach (var argument in node.Arguments)
            Next(argument, node);
    }

    private void Visit(SyntaxBlockNode node)
    {
        var parentScope = _syntaxTree.GetEnclosingBlock(node)?.Scope;
        if (node.Parent != null)
            Debug.Assert(parentScope != null);

        node.Scope = node.Parent switch
        {
            SyntaxClassDeclarationNode => new StructureScope(_syntaxTree.Namespace),
            null => _syntaxTree.Namespace,
            _ => new LocalScope(parentScope!),
        };

        foreach (var child in node.Expressions)
            Next(child, node);
    }

    private void Visit(SyntaxFunctionDeclarationNode node)
    {
        foreach (var parameter in node.Parameters)
            Next(parameter, node);

        if (node.ReturnType != null)
        {
            Next(node.ReturnType, node);
            ResolveNode(node.ReturnType);
        }

        Next(node.Body, node);
    }

    private void Visit(SyntaxClassDeclarationNode node)
    {
        foreach (var declaration in node.Declarations)
            Next(declaration, node);

        // This needs to be done after the declarations since
        // init parameters may refer to fields
        if (node.Init != null)
            Next(node.Init, node);
    }

    private void Visit(SyntaxFieldDeclarationNode node)
    {
        ResolveNode(node.Type);
        if (node.Value != null)
            Next(node.Value, node);
    }

    private void Visit(SyntaxInitNode node, StructureScope structureScope)
    {
        foreach (var parameter in node.Parameters)
        {
            parameter.Parent = node;
            Visit(parameter, structureScope);
        }

        Next(node.Body, node);
    }

    private void Visit(SyntaxInitParameterNode node, StructureScope structureScope)
    {
        if (node.Type != null)
        {
            Next(node.Type, node);
            ResolveNode(node.Type);

            return;
        }

        var symbol = structureScope.FindSymbol(node.Identifier.Value);
        if (symbol is not FieldSymbol fieldSymbol)
        {
            _diagnostics.ReportInitParameterFieldNotFound(node.Identifier);

            return;
        }

        node.LinkedField = fieldSymbol.SyntaxDeclaration;
    }
}
