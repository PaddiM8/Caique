using Caique.Analysis;

namespace Caique.Backend;

public class LlvmContextCache
{
    private readonly Dictionary<SemanticNode, string> _symbolNameCache = [];
    private readonly Dictionary<(ISemanticStructureDeclaration, ISemanticStructureDeclaration), string> _vtableNameCache = [];

    public void SetSymbolName(SemanticNode node, string name)
    {
        _symbolNameCache[node] = name;
    }

    public string GetSymbolName(SemanticNode node)
    {
        return _symbolNameCache[node];
    }

    public void SetVtableName(
        ISemanticStructureDeclaration implementor,
        ISemanticStructureDeclaration implemented,
        string name
    )
    {
        _vtableNameCache[(implementor, implemented)] = name;
    }

    public string GetVtableName(ISemanticStructureDeclaration implementor, ISemanticStructureDeclaration implemented)
    {
        return _vtableNameCache[(implementor, implemented)];
    }
}
