using System.Collections.Concurrent;
using Caique.Analysis;
using Caique.Scope;

namespace Caique.Lowering;

record OnDemandGeneratedFunctionEntry(
    FunctionSymbol Symbol,
    string UnqualifiedName,
    LoweredFunctionDeclarationNode Declaration
);

record OnDemandGeneratedVirtualFunctionEntry(
    FunctionSymbol ImplementationSymbol,
    FunctionDataType VirtualFunctionDataType,
    List<IDataType> StructureTypeArguments,
    StructureSymbol InstanceSymbol,
    TypeArgumentResolver TypeArgumentResolver,
    Func<TypeArgumentResolver, LoweredStructDeclarationNode, (LoweredFunctionDeclarationNode?, string?)> GenerateDeclaration
);

public record TypeListSpot(List<LoweredStructDataTypeField> TypeList, LoweredStructDataTypeField Placeholder);

public record ReferenceListSpot(List<LoweredNode> ReferenceList, LoweredOnDemandReferencePlaceholderNode Placeholder);

public class GlobalLoweringContext
{
    // Would've been a ConcurrentSet if that was a thing
    public ConcurrentDictionary<string, bool> AlreadyGeneratedSpecialisationsOfVirtualMethods { get; } = [];

    private readonly ConcurrentDictionary<string, bool> _alreadyGeneratedVtables = [];

    // Generics
    private readonly HashSet<string> _keysForAlreadyGeneratedStructs = [];
    private readonly HashSet<string> _keysForAlreadyGeneratedFunctions = [];
    private readonly Dictionary<FileScope, List<LoweredStructDeclarationNode>> _onDemandGeneratedStructsByFileScope = [];
    private readonly Dictionary<FileScope, List<OnDemandGeneratedFunctionEntry>> _onDemandGeneratedFunctionsByFileScope = [];
    private readonly Dictionary<FileScope, List<OnDemandGeneratedVirtualFunctionEntry>> _onDemandGeneratedVirtualFunctionsByFileScope = [];
    private readonly Lock _onDemandStructGenerationLock = new();
    private readonly Lock _onDemandFunctionGenerationLock = new();
    private readonly Lock _onDemandVirtualFunctionGenerationLock = new();

    private readonly Dictionary<StructureSymbol, List<LoweredStructDeclarationNode>> _generatedStructureDeclarationsBySymbol = [];

    // Would've been a ConcurrentSet if that was a thing
    private readonly ConcurrentDictionary<(FunctionSymbol, ReferenceListSpot), bool> _incompleteFunctionReferenceLists = [];
    private readonly ConcurrentDictionary<(FunctionSymbol, TypeListSpot), bool> _incompleteFunctionTypeLists = [];

    public void RegisterVtable(string name)
    {
        _alreadyGeneratedVtables[name] = true;
    }

    public bool VtableHasBeenRegistered(string name)
    {
        return _alreadyGeneratedVtables.ContainsKey(name);
    }

    /// <summary>
    /// Generic structures are generated on-demand since the concrete types are not yet known
    /// before the lowering stage. When, for example, a generic SemanticNewNode is encountered,
    /// a struct for the declaration is generated with the specific type arguments. Since the
    /// SemanticNewNode could be in a different SemanticTree, the struct is added to the global
    /// lowering context, and then added to the correct tree right before the emitting phase.
    ///
    /// If a struct has already been generated with the given type arguments, the
    /// <ref>generateDeclaration</ref> function is not called.
    /// </summary>
    public void AddOnDemandGenericStructure(
        StructureSymbol symbol,
        string name,
        FileScope fileScope,
        Func<LoweredStructDeclarationNode> generateDeclaration
    )
    {
        lock (_onDemandStructGenerationLock)
        {
            if (_keysForAlreadyGeneratedStructs.Contains(name))
                return;

            _keysForAlreadyGeneratedStructs.Add(name);

            var declaration = generateDeclaration();
            if (_onDemandGeneratedStructsByFileScope.TryGetValue(fileScope, out var entries))
            {
                entries.Add(declaration);
            }
            else
            {
                _onDemandGeneratedStructsByFileScope[fileScope] = [declaration];
            }

            if (_generatedStructureDeclarationsBySymbol.TryGetValue(symbol, out var entriesForSymbol))
            {
                entriesForSymbol.Add(declaration);
            }
            else
            {
                _generatedStructureDeclarationsBySymbol[symbol] = [declaration];
            }
        }
    }

    public void AddPregeneratedStructure(StructureSymbol symbol, LoweredStructDeclarationNode declaration)
    {
        lock (_onDemandStructGenerationLock)
        {
            if (_generatedStructureDeclarationsBySymbol.TryGetValue(symbol, out var entriesForSymbol))
            {
                entriesForSymbol.Add(declaration);
            }
            else
            {
                _generatedStructureDeclarationsBySymbol[symbol] = [declaration];
            }
        }
    }

    /// <summary>
    /// Generic functions are generated on-demand since the concrete types are not yet known
    /// before the lowering stage. When, for example, a generic call node is encountered, a
    /// function for the declaration is generated with the specific type arguments. Since the
    /// call node could be in a different SemanticTree, the function is added to the global
    /// lowering context, and then added to the correct tree right before the emitting phase.
    ///
    /// If a function has already been generated with the given type arguments, the
    /// <ref>generateDeclaration</ref> function is not called.
    /// </summary>
    public void AddOnDemandGenericFunction(
        FunctionSymbol symbol,
        string qualifiedName,
        string unqualifiedName,
        FileScope fileScope,
        Func<LoweredFunctionDeclarationNode> generateDeclaration
    )
    {
        lock (_onDemandFunctionGenerationLock)
        {
            if (_keysForAlreadyGeneratedFunctions.Contains(qualifiedName))
                return;

            _keysForAlreadyGeneratedFunctions.Add(qualifiedName);

            var declaration = generateDeclaration.Invoke();
            var entry = new OnDemandGeneratedFunctionEntry(symbol, unqualifiedName, declaration);
            if (_onDemandGeneratedFunctionsByFileScope.TryGetValue(fileScope, out var entries))
            {
                entries.Add(entry);
            }
            else
            {
                _onDemandGeneratedFunctionsByFileScope[fileScope] = [entry];
            }
        }
    }

    public void AddOnDemandGenericFunctionForAllSpecialisationsOfStructure(
        FunctionSymbol implementationSymbol,
        FileScope fileScope,
        FunctionDataType virtualFunctionDataType,
        StructureSymbol structureSymbol,
        List<IDataType> structureTypeArguments,
        TypeArgumentResolver typeArgumentResolver,
        Func<TypeArgumentResolver, LoweredStructDeclarationNode, (LoweredFunctionDeclarationNode?, string?)> generateDeclaration
    )
    {
        lock (_onDemandVirtualFunctionGenerationLock)
        {
            // Caching is handled in the lowerer
            var entry = new OnDemandGeneratedVirtualFunctionEntry(
                implementationSymbol,
                virtualFunctionDataType,
                structureTypeArguments,
                structureSymbol,
                (TypeArgumentResolver)typeArgumentResolver.Clone(),
                generateDeclaration
            );
            if (_onDemandGeneratedVirtualFunctionsByFileScope.TryGetValue(fileScope, out var entries))
            {
                entries.Add(entry);
            }
            else
            {
                _onDemandGeneratedVirtualFunctionsByFileScope[fileScope] = [entry];
            }
        }
    }

    /// <summary>
    /// When (for example) building vtables, it is necessary to fully resolve any generic functions.
    /// However, since these functions are generated on-demand, they will not be fully resolved until
    /// after the lowering phase is completely done. This means that things like vtables will need to
    /// be finished off after the lowering phase, since there is not enough information during it.
    /// To make this possible, we save a reference to the vtable's function reference list together
    /// with the function's symbol, so that we later can gather all the concrete lowered function
    /// declarations and complete the vtable, right before starting the code generation phase.
    /// </summary>
    public void AddIncompleteFunctionReferenceList(FunctionSymbol functionSymbol, ReferenceListSpot referenceListSpot)
    {
        _incompleteFunctionReferenceLists.TryAdd((functionSymbol, referenceListSpot), true);
    }

    /// <summary>
    /// When (for example) building vtable data types, it is necessary to fully resolve any generic functions.
    /// However, since these functions are generated on-demand, they will not be fully resolved until
    /// after the lowering phase is completely done. This means that things like vtables will need to
    /// be finished off after the lowering phase, since there is not enough information during it.
    /// To make this possible, we save a LoweredFunctionDataType to the vtable type's field type list
    /// together with the function's symbol, so that we later can gather all the concrete lowered function
    /// declarations and complete the vtable, right before starting the code generation phase.
    /// </summary>
    public void AddIncompleteFunctionTypeList(FunctionSymbol functionSymbol, TypeListSpot functionTypes)
    {
        _incompleteFunctionTypeLists.TryAdd((functionSymbol, functionTypes), true);
    }

    public IEnumerable<LoweredStructDeclarationNode> GetOnDemandGeneratedStructsForFile(FileScope fileScope)
    {
        lock (_onDemandStructGenerationLock)
        {
            _onDemandGeneratedStructsByFileScope.TryGetValue(fileScope, out var structs);

            return structs ?? [];
        }
    }

    public IEnumerable<LoweredFunctionDeclarationNode> GetOnDemandGeneratedFunctionsForFile(FileScope fileScope)
    {
        List<OnDemandGeneratedFunctionEntry>? functionEntries;
        lock (_onDemandFunctionGenerationLock)
        {
            _onDemandGeneratedFunctionsByFileScope.TryGetValue(fileScope, out functionEntries);
        }

        functionEntries ??= [];

        List<OnDemandGeneratedFunctionEntry> virtualFunctionEntries = [];
        HashSet<OnDemandGeneratedFunctionEntry> protocolFunctionEntries = [];
        lock (_onDemandVirtualFunctionGenerationLock)
        {
            _onDemandGeneratedVirtualFunctionsByFileScope.TryGetValue(fileScope, out var ungeneratedVirtualFunctions);
            foreach (var ungeneratedVirtualFunction in ungeneratedVirtualFunctions ?? [])
            {
                if (!_generatedStructureDeclarationsBySymbol.TryGetValue(ungeneratedVirtualFunction.InstanceSymbol, out var generatedStructDeclarations))
                    continue;

                foreach (var structDeclaration in generatedStructDeclarations)
                {
                    ungeneratedVirtualFunction.TypeArgumentResolver.PushTypeArguments(
                        ungeneratedVirtualFunction.StructureTypeArguments,
                        ungeneratedVirtualFunction.InstanceSymbol.SemanticDeclaration!
                    );
                    ungeneratedVirtualFunction.TypeArgumentResolver.PushTypeArguments(
                        ungeneratedVirtualFunction.VirtualFunctionDataType.TypeArguments,
                        ungeneratedVirtualFunction.ImplementationSymbol.SemanticDeclaration!
                    );

                    var (generatedDeclaration, unqualifiedName) = ungeneratedVirtualFunction.GenerateDeclaration(
                        ungeneratedVirtualFunction.TypeArgumentResolver,
                        structDeclaration
                    );
                    if (generatedDeclaration == null || unqualifiedName == null)
                        continue;

                    var entry = new OnDemandGeneratedFunctionEntry(
                        ungeneratedVirtualFunction.ImplementationSymbol,
                        unqualifiedName,
                        generatedDeclaration
                    );
                    virtualFunctionEntries.Add(entry);

                    // A type like SomeProtocol.vtable.SomeProtocol might be generated for protocols,
                    // to provide a generic type that can be used when the concrete type is not known.
                    // This means that, in the case of protocols, we also need to process the protocol's
                    // functions here in order to fill out the type list.
                    if (ungeneratedVirtualFunction.VirtualFunctionDataType.InstanceDataType?.IsProtocol() is true)
                    {
                        var virtualEntry = new OnDemandGeneratedFunctionEntry(
                            ungeneratedVirtualFunction.VirtualFunctionDataType.Symbol,
                            unqualifiedName,
                            generatedDeclaration
                        );
                        protocolFunctionEntries.Add(virtualEntry);
                    }

                    ungeneratedVirtualFunction.TypeArgumentResolver.PopTypeArguments();
                    ungeneratedVirtualFunction.TypeArgumentResolver.PopTypeArguments();
                }
            }
        }

        var allEntries = functionEntries
            .Concat(virtualFunctionEntries)
            .Concat(protocolFunctionEntries);
        ResolveIncompleteLists(allEntries);

        return allEntries.Select(x => x.Declaration);
    }

    private void ResolveIncompleteLists(IEnumerable<OnDemandGeneratedFunctionEntry> onDemandGeneratedFunctions)
    {
        var referenceListsBySymbol = new Dictionary<FunctionSymbol, List<ReferenceListSpot>>();
        foreach (var (functionSymbol, functionReferenceList) in _incompleteFunctionReferenceLists.Keys)
        {
            if (referenceListsBySymbol.TryGetValue(functionSymbol, out var lists))
            {
                lists.Add(functionReferenceList);
            }
            else
            {
                referenceListsBySymbol[functionSymbol] = [functionReferenceList];
            }
        }

        var typeListsBySymbol = new Dictionary<FunctionSymbol, List<TypeListSpot>>();
        foreach (var (functionSymbol, functionTypeList) in _incompleteFunctionTypeLists.Keys)
        {
            if (typeListsBySymbol.TryGetValue(functionSymbol, out var lists))
            {
                lists.Add(functionTypeList);
            }
            else
            {
                typeListsBySymbol[functionSymbol] = [functionTypeList];
            }
        }

        foreach (var function in onDemandGeneratedFunctions)
        {
            if (referenceListsBySymbol.TryGetValue(function.Symbol, out var functionReferenceLists))
            {
                foreach (var functionReferenceList in functionReferenceLists)
                {
                    var functionReference = new LoweredFunctionReferenceNode(
                        function.Declaration.Identifier,
                        new LoweredPointerDataType(function.Declaration.DataType)
                    );
                    var index = functionReferenceList.ReferenceList.IndexOf(functionReferenceList.Placeholder);
                    functionReferenceList.ReferenceList.Insert(index, functionReference);
                }
            }

            if (typeListsBySymbol.TryGetValue(function.Symbol, out var functionTypeLists))
            {
                foreach (var functionTypeList in functionTypeLists)
                {
                    var functionDataType = new LoweredPointerDataType(
                        (LoweredFunctionDataType)function.Declaration.DataType
                    );
                    var index = functionTypeList.TypeList.IndexOf(functionTypeList.Placeholder);
                    var field = new LoweredStructDataTypeField(function.UnqualifiedName, functionDataType);
                    functionTypeList.TypeList.Insert(index, field);
                }
            }
        }
    }
}
