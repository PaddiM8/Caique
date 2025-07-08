using LLVMSharp.Interop;

namespace Caique.Backend;

public static class LlvmUtils
{
    public static LLVMTypeRef Int128Type(LLVMContextRef context)
    {
        unsafe
        {
            return LLVM.Int128TypeInContext(context);
        }
    }

    public static LLVMValueRef CreateConstBool(LLVMContextRef context, bool value)
    {
        ulong numericalValue = value ? (uint)1 : 0;

        return LLVMValueRef.CreateConstInt(context.Int1Type, numericalValue, SignExtend: false);
    }


    public static LLVMValueRef BuildSizeOf(LLVMContextRef context, LLVMModuleRef module, LLVMTypeRef type)
    {
        ulong size;
        unsafe
        {
            size = LLVMTargetDataRef.FromStringRepresentation(module.DataLayout).StoreSizeOfType(type);
        }

        return LLVMValueRef.CreateConstInt(context.Int64Type, size);
    }
}
