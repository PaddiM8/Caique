using Caique.Analysis;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmModuleCache
{
    private readonly Dictionary<SemanticNode, LLVMValueRef> _llvmValueLookup = [];
    private readonly Dictionary<SemanticNode, LLVMTypeRef> _llvmTypeLookup = [];
    private readonly Dictionary<SemanticBlockNode, LLVMBasicBlockRef> _llvmBlockValueLookup = [];

    public LLVMValueRef GetNodeLlvmValue(SemanticNode node)
    {
        return _llvmValueLookup[node];
    }

    public void SetNodeLlvmValue(SemanticNode node, LLVMValueRef value)
    {
        _llvmValueLookup[node] = value;
    }

    public LLVMBasicBlockRef GetBlockLlvmValue(SemanticBlockNode node)
    {
        return _llvmBlockValueLookup[node];
    }

    public void SetBlockLlvmValue(SemanticBlockNode node, LLVMBasicBlockRef value)
    {
        _llvmBlockValueLookup[node] = value;
    }
}
