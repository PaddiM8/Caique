using Caique.Scope;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmEmitterContext(string moduleName, LLVMContextRef context, LlvmContextCache contextCache) : IDisposable
{
    public string ModuleName { get; } = moduleName;

    public LlvmContextCache ContextCache { get; } = contextCache;

    public LlvmModuleCache ModuleCache { get; } = new LlvmModuleCache();

    public LlvmTypeBuilder LlvmTypeBuilder { get; } = new LlvmTypeBuilder(context);

    public LLVMContextRef LlvmContext { get; } = context;

    public LLVMBuilderRef LlvmBuilder { get; } = LLVMBuilderRef.Create(context);

    public LLVMModuleRef LlvmModule { get; } = LLVMModuleRef.CreateWithName(moduleName);

    public void Dispose()
    {
        LlvmBuilder.Dispose();
        LlvmModule.Dispose();
    }
}
