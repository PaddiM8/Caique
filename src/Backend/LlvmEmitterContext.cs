using Caique.Scope;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmEmitterContext(string moduleName, LLVMContextRef context) : IDisposable
{
    public string ModuleName { get; } = moduleName;

    public LlvmTypeBuilder LlvmTypeBuilder { get; } = new LlvmTypeBuilder(context);

    public LLVMContextRef LlvmContext { get; } = context;

    public LLVMBuilderRef LlvmBuilder { get; } = context.CreateBuilder();

    public LLVMModuleRef LlvmModule { get; } = context.CreateModuleWithName(moduleName);

    public void Dispose()
    {
        LlvmBuilder.Dispose();
        LlvmModule.Dispose();
    }
}
