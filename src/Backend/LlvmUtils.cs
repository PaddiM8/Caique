using LLVMSharp.Interop;

namespace Caique.Backend;

public static class LlvmUtils
{
    public static LLVMTypeRef Int128
    {
        get
        {
            unsafe
            {
                return LLVM.Int128Type();
            }
        }
    }

    public static LLVMValueRef CreateConstBool(bool value)
    {
        ulong numericalValue = value ? (uint)1 : 0;

        return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, numericalValue, SignExtend: false);
    }
}
