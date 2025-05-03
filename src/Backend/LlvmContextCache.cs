using Caique.Analysis;

namespace Caique.Backend;

public class LlvmContextCache
{
    private readonly Dictionary<SemanticNode, string> _symbolNameCache = [];

    public void SetSymbolName(SemanticNode node, string name)
    {
        _symbolNameCache[node] = name;
    }

    public string GetSymbolName(SemanticNode node)
    {
        return _symbolNameCache[node];
    }
}
