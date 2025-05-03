using Caique.Analysis;
using Caique.Scope;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmHeaderEmitter
{
    private readonly SemanticTree _tree;
    private readonly LlvmContextCache _contextCache;
    private readonly LlvmModuleCache _moduleCache;
    private readonly LlvmTypeBuilder _typeBuilder;
    private readonly LLVMContextRef _context;
    private readonly LLVMBuilderRef _builder;
    private readonly LLVMModuleRef _module;

    private LlvmHeaderEmitter(SemanticTree tree, LlvmEmitterContext emitterContext)
    {
        _tree = tree;
        _contextCache = emitterContext.ContextCache;
        _moduleCache = emitterContext.ModuleCache;
        _typeBuilder = emitterContext.LlvmTypeBuilder;
        _context = emitterContext.LlvmContext;
        _builder = emitterContext.LlvmBuilder;
        _module = emitterContext.LlvmModule;
    }

    public static void Emit(SemanticTree tree, LlvmEmitterContext emitterContext)
    {
        var emitter = new LlvmHeaderEmitter(tree, emitterContext);
        emitter.Next(tree.Root);
    }

    private void Next(SemanticNode node)
    {
        switch (node)
        {
            case SemanticBlockNode blockNode:
                Visit(blockNode);
                return;
            case SemanticFunctionDeclarationNode functionNode:
                Visit(functionNode);
                return;
            case SemanticClassDeclarationNode classNode:
                Visit(classNode);
                return;
        }
    }

    private void Visit(SemanticBlockNode node)
    {
        foreach (var expression in node.Expressions)
            Next(expression);
    }

    private void Visit(SemanticFunctionDeclarationNode node)
    {
        var functionType = _typeBuilder.BuildType(new FunctionDataType(node.Symbol));
        var function = _module.AddFunction(node.Identifier.Value, functionType);
        _moduleCache.SetNodeLlvmValue(node, function);
        _contextCache.SetSymbolName(node, node.Identifier.Value);
    }

    private void Visit(SemanticClassDeclarationNode node)
    {
        Visit(node.Init, node);

        foreach (var function in node.Functions)
            Next(function);
    }

    private void Visit(SemanticInitNode node, ISemanticStructureDeclaration parentStructure)
    {
        var functionType = _typeBuilder.BuildInitType(node);
        var identifier = $"{parentStructure.Identifier.Value}::{parentStructure.Identifier.Value}";
        var function = _module.AddFunction(identifier, functionType);
        _moduleCache.SetNodeLlvmValue(node, function);
        _contextCache.SetSymbolName(node, identifier);
    }
}
