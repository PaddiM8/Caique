using Caique.Scope;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmEmitterContext(string moduleName, LLVMContextRef context, LlvmCache globalCache) : IDisposable
{
    public string ModuleName { get; } = moduleName;

    public LlvmCache GlobalCache { get; } = globalCache;

    public LLVMContextRef LlvmContext { get; } = context;

    public LLVMBuilderRef LlvmBuilder { get; } = LLVMBuilderRef.Create(context);

    public LLVMModuleRef LlvmModule { get; } = LLVMModuleRef.CreateWithName(moduleName);

    public LlvmTypeBuilder LlvmTypeBuilder { get; } = new LlvmTypeBuilder(context);

    public void Dispose()
    {
        LlvmBuilder.Dispose();
        LlvmModule.Dispose();
    }
}
