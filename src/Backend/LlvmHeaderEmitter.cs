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
    private readonly LlvmSpecialValueBuilder _specialValueBuilder;

    private LlvmHeaderEmitter(SemanticTree tree, LlvmEmitterContext emitterContext)
    {
        _tree = tree;
        _contextCache = emitterContext.ContextCache;
        _moduleCache = emitterContext.ModuleCache;
        _typeBuilder = emitterContext.LlvmTypeBuilder;
        _context = emitterContext.LlvmContext;
        _builder = emitterContext.LlvmBuilder;
        _module = emitterContext.LlvmModule;
        _specialValueBuilder = new LlvmSpecialValueBuilder(emitterContext, _typeBuilder);
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
        var name = CreateFunctionName(node);
        var function = _module.AddFunction(name, functionType);
        _moduleCache.SetNodeLlvmValue(node, function);
        _contextCache.SetSymbolName(node, name);
    }

    private string CreateFunctionName(SemanticFunctionDeclarationNode node)
    {
        var ffiAttribute = node.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        if (ffiAttribute != null)
        {
            return node.Identifier.Value;
        }

        var parentStructure = _tree.GetEnclosingStructure(node);
        var parentType = new StructureDataType(parentStructure!.Symbol);

        return $"{parentType}:{node.Identifier.Value}";
    }

    private void Visit(SemanticClassDeclarationNode node)
    {
        Visit(node.Init, node);

        foreach (var staticField in node.Fields.Where(x => x.IsStatic))
            BuildStaticField(staticField, node);

        foreach (var function in node.Functions)
            Next(function);
    }

    private void BuildStaticField(SemanticFieldDeclarationNode field, ISemanticStructureDeclaration parentStructure)
    {
        var type = _typeBuilder.BuildType(field.DataType);
        var name = CreateFieldName(field, parentStructure);
        _contextCache.SetSymbolName(field, name);

        var global = _module.AddGlobal(type, name);
        if (!field.Attributes.Any(x => x.Identifier.Value == "ffi"))
        {
            global.Linkage = LLVMLinkage.LLVMInternalLinkage;
            global.Initializer = _specialValueBuilder.BuildDefaultValueForType(field.DataType);
        }

        _moduleCache.SetNodeLlvmValue(field, global);
    }

    private string CreateFieldName(SemanticFieldDeclarationNode field, ISemanticStructureDeclaration parentStructure)
    {
        var ffiAttribute = field.Attributes.FirstOrDefault(x => x.Identifier.Value == "ffi");
        var parentType = new StructureDataType(parentStructure.Symbol);
        var name = ffiAttribute == null
            ? $"{parentType}:{field.Identifier.Value}"
            : field.Identifier.Value;

        return name;
    }

    private void Visit(SemanticInitNode node, ISemanticStructureDeclaration parentStructure)
    {
        var functionType = _typeBuilder.BuildInitType(node);
        var parentType = new StructureDataType(parentStructure.Symbol);
        var identifier = $"{parentType}:{parentStructure.Identifier.Value}";
        var function = _module.AddFunction(identifier, functionType);
        _moduleCache.SetNodeLlvmValue(node, function);
        _contextCache.SetSymbolName(node, identifier);
    }
}
