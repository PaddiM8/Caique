using System.Collections.Concurrent;
using Caique.Analysis;
using Caique.Scope;

namespace Caique.Lowering;

public class GlobalLoweringContext
{
    private readonly HashSet<string> _keysForAlreadyGeneratedStructs = [];
    private readonly Dictionary<FileScope, List<LoweredStructDeclarationNode>> _lazilyGeneratedStructsByFileScope = [];
    private readonly Lock _lazyStructGenerationLock = new();

    /// <summary>
    /// Generic structs are generated on-demand since the concrete types are not yet known
    /// before the lowering stage. When, for example, a generic SemanticNewNode is encountered,
    /// a struct for the declaration is generated with the specific type arguments. Since the
    /// SemanticNewNode could be in a different SemanticTree, the struct is added to the global
    /// lowering context, and then added to the correct tree right before the emitting phase.
    ///
    /// If a struct has already been generated with the given type arguments, the
    /// <ref>generateDeclaraation</ref> function is not called.
    /// </summary>
    public void AddGenericStructLazily(
        LoweredStructDataType structDataType,
        FileScope fileScope,
        Func<LoweredStructDeclarationNode> generateDeclaration
    )
    {
        lock (_lazyStructGenerationLock)
        {
            if (_keysForAlreadyGeneratedStructs.Contains(structDataType.Name!))
                return;

            _keysForAlreadyGeneratedStructs.Add(structDataType.Name!);

            if (_lazilyGeneratedStructsByFileScope.TryGetValue(fileScope, out var entries))
            {
                entries.Add(generateDeclaration());
            }
            else
            {
                _lazilyGeneratedStructsByFileScope[fileScope] = [generateDeclaration()];
            }
        }
    }

    public List<LoweredStructDeclarationNode> GetLazilyGeneratedStructsForFile(FileScope fileScope)
    {
        lock (_lazyStructGenerationLock)
        {
            _lazilyGeneratedStructsByFileScope.TryGetValue(fileScope, out var structs);

            return structs ?? [];
        }
    }
}
