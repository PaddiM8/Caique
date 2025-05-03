using Caique.Analysis;
using Caique.Scope;
using LLVMSharp;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmHeaderEmitter
{
    private readonly SemanticTree _tree;
    private readonly LLVMContextRef _llvmContext;
    private readonly LLVMBuilderRef _llvmBuilder;
    private readonly LLVMModuleRef _llvmModule;
    private readonly LlvmTypeBuilder _llvmTypeBuilder;
    private readonly LlvmCache _globalCache;

    private LlvmHeaderEmitter(SemanticTree tree, LlvmEmitterContext emitterContext)
    {
        _tree = tree;
        _llvmContext = emitterContext.LlvmContext;
        _llvmBuilder = emitterContext.LlvmBuilder;
        _llvmModule = emitterContext.LlvmModule;
        _llvmTypeBuilder = emitterContext.LlvmTypeBuilder;
        _globalCache = emitterContext.GlobalCache;
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
        var functionType = _llvmTypeBuilder.BuildType(new FunctionDataType(node.Symbol));
        var functionValue = _llvmModule.AddFunction(node.Identifier.Value, functionType);
        _globalCache.SetNodeLlvmValue(node, functionValue);
    }

    private void Visit(SemanticClassDeclarationNode node)
    {
        Visit(node.Init, node);

        foreach (var function in node.Functions)
            Next(function);
    }

    private void Visit(SemanticInitNode node, ISemanticStructureDeclaration parentStructure)
    {
        var parameterTypes = node.Parameters
            .Select(x => x.DataType)
            .Select(x => _llvmTypeBuilder.BuildType(x))
            .ToArray();
        var returnType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0);

        var functionType = LLVMTypeRef.CreateFunction(returnType, parameterTypes);
        var identifier = $"{parentStructure.Identifier.Value}::{parentStructure.Identifier.Value}";
        var functionValue = _llvmModule.AddFunction(identifier, functionType);
        _globalCache.SetNodeLlvmValue(node, functionValue);
        _globalCache.SetNodeLlvmType(node, functionType);
    }
}
