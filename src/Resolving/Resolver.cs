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
                Next(statementNode.Expression, statementNode);
                break;
            case SyntaxUnaryNode unaryNode:
                Next(unaryNode.Value, unaryNode);
                break;
            case SyntaxBinaryNode binaryNode:
                Next(binaryNode.Left, binaryNode);
                Next(binaryNode.Right, binaryNode);
                break;
            case SyntaxCallNode callNode:
                Next(callNode.Left, callNode);
                foreach (var argument in callNode.Arguments)
                    Next(argument, callNode);

                break;
            case SyntaxBlockNode blockNode:
                Visit(blockNode);
                break;
            case SyntaxVariableDeclarationNode variableDeclarationNode:
                Next(variableDeclarationNode.Value, variableDeclarationNode);
                break;
            case SyntaxFunctionDeclarationNode functionDeclarationNode:
                Visit(functionDeclarationNode);
                break;
            case SyntaxParameterNode parameterNode:
                Next(parameterNode.Type, parameterNode);
                ResolveNode(parameterNode.Type);
                break;
            case SyntaxClassDeclarationNode classDeclarationNode:
                Visit(classDeclarationNode);
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

    private void Visit(SyntaxClassDeclarationNode classDeclarationNode)
    {
        // TODO: When fields exist, resolve the types of public ones here
        foreach (var declaration in classDeclarationNode.Declarations)
            Next(declaration, classDeclarationNode);
    }
}
