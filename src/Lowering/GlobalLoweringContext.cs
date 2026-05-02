using System.Collections.Concurrent;
using Caique.Analysis;
using Caique.Scope;

namespace Caique.Lowering;

record OnDemandGeneratedFunctionEntry(
    FunctionSymbol Symbol,
    string UnqualifiedName,
    LoweredFunctionDeclarationNode Declaration,
    string? StructureName,
    StructureSymbol? VirtualStructureSymbol
);

record OnDemandGeneratedVirtualFunctionEntry(
    FunctionSymbol ImplementationSymbol,
    FunctionDataType VirtualFunctionDataType,
    List<IDataType> StructureTypeArguments,
    StructureSymbol InstanceSymbol,
    StructureSymbol VirtualInstanceSymbol,
    TypeArgumentResolver TypeArgumentResolver,
    Func<TypeArgumentResolver, LoweredStructDeclarationNode, (LoweredFunctionDeclarationNode?, string?)> GenerateDeclaration
);

public record TypeListSpot(
    List<LoweredStructDataTypeField> TypeList,
    LoweredStructDataTypeField Placeholder,
    List<string> StructureNames,
    StructureSymbol VirtualStructureSymbol
);

public record ReferenceListSpot(
    List<LoweredNode> ReferenceList,
    LoweredOnDemandReferencePlaceholderNode Placeholder,
    List<string> StructureNames,
    StructureSymbol VirtualStructureSymbol
);

record GeneratedStructureEntry(
    LoweredStructDeclarationNode LoweredDeclaration,
    ISemanticStructureDeclaration SemanticDeclaration,
    List<IDataType> TypeArguments,
    TypeArgumentResolver TypeArgumentResolver
);

public class GlobalLoweringContext
{
    // Would've been a ConcurrentSet if that was a thing
    public ConcurrentDictionary<string, bool> AlreadyGeneratedSpecialisationsOfVirtualMethods { get; } = [];

    public ConcurrentDictionary<ISymbol, LoweredStructDataType> LoweredImplementationDataTypesForProtocol { get; } = [];

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

    private readonly Dictionary<StructureSymbol, List<GeneratedStructureEntry>> _generatedStructureDeclarationsBySymbol = [];

    // Would've been a ConcurrentSet if that was a thing
    private readonly ConcurrentDictionary<(FunctionSymbol, ReferenceListSpot), bool> _incompleteFunctionReferenceLists = [];
    private readonly ConcurrentDictionary<(FunctionSymbol, TypeListSpot), bool> _incompleteFunctionTypeLists = [];
    private readonly ConcurrentDictionary<(LoweredStructDataType, StructureSymbol), bool> _incompleteLoweredDataTypes = [];

    public void RegisterVtable(string name)
    {
        _alreadyGeneratedVtables[name] = true;
    }

    public bool VtableHasBeenRegistered(string name)
    {
        return _alreadyGeneratedVtables.ContainsKey(name);
    }

    /// <summary>
    /// <para>
    /// Generic structures are generated on-demand since the concrete types are not yet known
    /// before the lowering stage. When, for example, a generic SemanticNewNode is encountered,
    /// a struct for the declaration is generated with the specific type arguments. Since the
    /// SemanticNewNode could be in a different SemanticTree, the struct is added to the global
    /// lowering context, and then added to the correct tree right before the emitting phase.
    /// </para>
    /// <para>
    /// If a struct has already been generated with the given type arguments, the
    /// <ref>generateDeclaration</ref> function is not called.
    /// </para>
    /// </summary>
    public void AddOnDemandGenericStructure(
        StructureSymbol symbol,
        string name,
        List<IDataType> typeArguments,
        FileScope fileScope,
        Func<(LoweredStructDeclarationNode, TypeArgumentResolver)> generateDeclaration
    )
    {
        lock (_onDemandStructGenerationLock)
        {
            if (_keysForAlreadyGeneratedStructs.Contains(name))
                return;

            _keysForAlreadyGeneratedStructs.Add(name);

            var (declaration, typeArgumentResolver) = generateDeclaration();
            if (_onDemandGeneratedStructsByFileScope.TryGetValue(fileScope, out var entries))
            {
                entries.Add(declaration);
            }
            else
            {
                _onDemandGeneratedStructsByFileScope[fileScope] = [declaration];
            }

            var entry = new GeneratedStructureEntry(declaration, symbol.SemanticDeclaration!, typeArguments, typeArgumentResolver);
            if (_generatedStructureDeclarationsBySymbol.TryGetValue(symbol, out var entriesForSymbol))
            {
                entriesForSymbol.Add(entry);
            }
            else
            {
                _generatedStructureDeclarationsBySymbol[symbol] = [entry];
            }
        }
    }

    public void AddPregeneratedStructure(StructureSymbol symbol, LoweredStructDeclarationNode declaration, TypeArgumentResolver typeArgumentResolver)
    {
        lock (_onDemandStructGenerationLock)
        {
            var entry = new GeneratedStructureEntry(
                declaration,
                symbol.SemanticDeclaration!,
                [],
                (TypeArgumentResolver)typeArgumentResolver.Clone()
            );
            if (_generatedStructureDeclarationsBySymbol.TryGetValue(symbol, out var entriesForSymbol))
            {
                entriesForSymbol.Add(entry);
            }
            else
            {
                _generatedStructureDeclarationsBySymbol[symbol] = [entry];
            }
        }
    }

    /// <summary>
    /// <para>
    /// Generic functions are generated on-demand since the concrete types are not yet known
    /// before the lowering stage. When, for example, a generic call node is encountered, a
    /// function for the declaration is generated with the specific type arguments. Since the
    /// call node could be in a different SemanticTree, the function is added to the global
    /// lowering context, and then added to the correct tree right before the emitting phase.
    /// </para>
    /// <para>
    /// If a function has already been generated with the given type arguments, the
    /// <ref>generateDeclaration</ref> function is not called.
    /// </para>
    /// </summary>
    public void AddOnDemandGenericFunction(
        FunctionSymbol symbol,
        string qualifiedName,
        string unqualifiedName,
        FileScope fileScope,
        string? structureName,
        StructureSymbol? structureSymbol,
        Func<LoweredFunctionDeclarationNode> generateDeclaration
    )
    {
        lock (_onDemandFunctionGenerationLock)
        {
            if (_keysForAlreadyGeneratedFunctions.Contains(qualifiedName))
                return;

            _keysForAlreadyGeneratedFunctions.Add(qualifiedName);

            var declaration = generateDeclaration.Invoke();
            var entry = new OnDemandGeneratedFunctionEntry(
                symbol,
                unqualifiedName,
                declaration,
                structureName,
                structureSymbol
            );
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
        StructureSymbol virtualInstanceSymbol,
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
                virtualInstanceSymbol,
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

    public void AddIncompleteStructDataType(LoweredStructDataType dataType, StructureSymbol symbol)
    {
        _incompleteLoweredDataTypes.TryAdd((dataType, symbol), true);
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

        // During the regular lowering phase, virtual generic methods can not be generated, because it is not yet known which
        // concrete structures could be associated with it. For example, if the classes A and B both implement the protocol P,
        // which has a generic method, all the specialisations of the generic method have to be added to A and B. Therefore,
        // we have to wait until after all the lowering is done (for all files) before being able to generate all the generic
        // virtual methods, in order to know which concrete structures they should be added to.
        //
        // Further more, when the functions are added to classes, the vtables also have to be updated to include the new
        // functions. When the vtables are first generated, placeholders are added in the place of generic virtual methods,
        // so that they can be resolved later at this stage instead.
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
                    int popCount = ungeneratedVirtualFunction.TypeArgumentResolver.PushAllFromOtherResolver(structDeclaration.TypeArgumentResolver);
                    ungeneratedVirtualFunction.TypeArgumentResolver.PushTypeArguments(
                        structDeclaration.TypeArguments,
                        structDeclaration.SemanticDeclaration
                    );
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
                        structDeclaration.LoweredDeclaration
                    );
                    if (generatedDeclaration == null || unqualifiedName == null)
                        continue;

                    var entry = new OnDemandGeneratedFunctionEntry(
                        ungeneratedVirtualFunction.ImplementationSymbol,
                        unqualifiedName,
                        generatedDeclaration,
                        structDeclaration.LoweredDeclaration.Identifier,
                        ungeneratedVirtualFunction.VirtualInstanceSymbol
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
                            generatedDeclaration,
                            ungeneratedVirtualFunction.VirtualFunctionDataType.InstanceDataType.QualifiedName,
                            ungeneratedVirtualFunction.VirtualFunctionDataType.InstanceDataType.Symbol
                        );
                        protocolFunctionEntries.Add(virtualEntry);
                    }

                    ungeneratedVirtualFunction.TypeArgumentResolver.PopTypeArguments();
                    ungeneratedVirtualFunction.TypeArgumentResolver.PopTypeArguments();
                    ungeneratedVirtualFunction.TypeArgumentResolver.PopTypeArguments();

                    for (var i = 0; i < popCount; i++)
                        ungeneratedVirtualFunction.TypeArgumentResolver.PopTypeArguments();
                }
            }
        }

        var allEntries = functionEntries.Concat(virtualFunctionEntries);
        ResolveIncompleteLists(allEntries.Concat(protocolFunctionEntries));

        return allEntries.Select(x => x.Declaration);
    }

    /// <summary>
    /// Generic virtual methods are generated *after* the regular lowering phase has been completed in all files.
    /// This means that those functions have not yet been added to the parent structures and vtables during the
    /// lowering phase. Instead, placeholders are inserted into the vtables during lowering, which are then
    /// resolved later on, in this method, when all the concrete implementing structures of the protocol/parent class
    /// have been already found and generated.
    /// </summary>
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
                    // For protocols, one vtable is generated per protocol and implementor, eg. SomeClass.vtable.SomeProtocol. This means
                    // that some generated functions will belong to a specific protocol-implementor vtable. Because of this, we filter by
                    // the virtual structure type here. If we did not do this, the resulting vtable could include references to functions
                    // that were used on other protocols implemented by the structure.
                    //
                    // Note that this only applies to protocols, since eg. inherited classes only have one vtable.
                    if (functionReferenceList.VirtualStructureSymbol.SemanticDeclaration is not ISemanticInstantiableStructureDeclaration &&
                        functionReferenceList.VirtualStructureSymbol != function.VirtualStructureSymbol)
                    {
                        continue;
                    }

                    if (function.StructureName == null || !functionReferenceList.StructureNames.Contains(function.StructureName))
                        continue;

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
                    // For protocols, one vtable is generated per protocol and implementor, eg. SomeClass.vtable.SomeProtocol. This means
                    // that some generated functions will belong to a specific protocol-implementor vtable. Because of this, we filter by
                    // the virtual structure type here. If we did not do this, the resulting vtable could include references to functions
                    // that were used on other protocols implemented by the structure.
                    //
                    // Note that this only applies to protocols, since eg. inherited classes only have one vtable.
                    if (functionTypeList.VirtualStructureSymbol.SemanticDeclaration is not ISemanticInstantiableStructureDeclaration &&
                        functionTypeList.VirtualStructureSymbol != function.VirtualStructureSymbol)
                    {
                        continue;
                    }

                    if (function.StructureName == null || !functionTypeList.StructureNames.Contains(function.StructureName))
                        continue;

                    var functionDataType = new LoweredPointerDataType(
                        (LoweredFunctionDataType)function.Declaration.DataType
                    );
                    var index = functionTypeList.TypeList.IndexOf(functionTypeList.Placeholder);
                    var field = new LoweredStructDataTypeField(function.UnqualifiedName, functionDataType);
                    functionTypeList.TypeList.Insert(index, field);
                }
            }
        }

        // TODO: Document this
        // TODO: Doing Fields.AddRange(...) instead of Fields = ... broke another test... hmm
        // TODO: B.vtable.P should probably have entries?
        //       ^ for it do have entries, is it not necessary to change the system with functionTypeList.StructureName and all that?
        //       The structure name in one case will be A, which B inherits from, and in the other case it will be B, so it won't match,
        //       but it should I think..?
        //       Or maybe it would be possible to simply use A.vtable.P instead and not have B.vtable.P? <-- probably not
        foreach (var (incompleteDataType, symbol) in _incompleteLoweredDataTypes.Keys)
            incompleteDataType.Fields = LoweredImplementationDataTypesForProtocol[symbol].Fields;
    }
}
