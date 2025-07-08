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
            case SemanticModuleDeclarationNode moduleNode:
                Visit(moduleNode);
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
        var functionType = _typeBuilder.BuildFunctionType(node.Symbol);
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

        CreateTypeTable(node);

        foreach (var subType in node.ImplementedProtocols)
            CreateVtable(node, subType.SemanticDeclaration!);
    }

    private void CreateVtable(ISemanticStructureDeclaration implementor, ISemanticStructureDeclaration implemented)
    {
        var vtableType = _typeBuilder.BuildVtableType(implemented.Symbol);
        var functionPointers = implementor
            .Functions
            .Select(x => _moduleCache.GetNodeLlvmValue(x))
            .ToArray();

        var structValue = LLVMValueRef.CreateConstNamedStruct(vtableType, functionPointers);
        var implementorType = new StructureDataType(implementor.Symbol);
        var implementedType = new StructureDataType(implemented.Symbol);
        var name = $"{implementorType}.vtable.{implementedType}";
        var global = _module.AddGlobal(vtableType, name);
        global.Initializer = structValue;
        global.IsGlobalConstant = true;

        _contextCache.SetVtableName(implementor, implemented, name);
    }

    private void CreateTypeTable(SemanticClassDeclarationNode classNode)
    {
        // Prepare the vtable
        CreateVtable(classNode, classNode);
        var vtableName = _contextCache.GetVtableName(classNode, classNode);
        var vtablePointer = _module.GetNamedGlobal(vtableName);

        // Build the type table
        var typeTableType = _typeBuilder.BuildTypeTableType(classNode.Symbol);
        var fields = new LLVMValueRef[]
        {
            vtablePointer,
        };

        var structValue = LLVMValueRef.CreateConstNamedStruct(typeTableType, fields);
        var dataType = new StructureDataType(classNode.Symbol);
        var name = $"typeTable.{dataType}";
        var global = _module.AddGlobal(typeTableType, name);
        global.Initializer = structValue;
        global.IsGlobalConstant = true;

        _contextCache.SetTypeTableName(classNode, name);
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

    private void Visit(SemanticModuleDeclarationNode node)
    {
         foreach (var staticField in node.Fields.Where(x => x.IsStatic))
            BuildStaticField(staticField, node);

        foreach (var function in node.Functions)
            Next(function);
    }
}
