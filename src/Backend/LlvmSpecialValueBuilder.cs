using System.Diagnostics;
using Caique.Analysis;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmSpecialValueBuilder(LlvmEmitterContext emitterContext, LlvmTypeBuilder llvmTypeBuilder)
{
    private readonly LLVMContextRef _llvmContext = emitterContext.LlvmContext;
    private readonly LLVMModuleRef _llvmModule = emitterContext.LlvmModule;
    private readonly LLVMBuilderRef _llvmBuilder = emitterContext.LlvmBuilder;
    private readonly LlvmTypeBuilder _llvmTypeBuilder = llvmTypeBuilder;

    public LLVMValueRef BuildLogicalAnd(LLVMValueRef left, Func<LLVMValueRef> getRight)
    {
        var function = _llvmBuilder.InsertBlock.Parent;
        var thenBlock = function.AppendBasicBlock("and.then");
        var elseBlock = function.AppendBasicBlock("and.else");
        var mergeBlock = function.AppendBasicBlock("and.merge");

        _llvmBuilder.BuildCondBr(left, thenBlock, elseBlock);

        // Then
        _llvmBuilder.PositionAtEnd(thenBlock);
        var right = getRight();
        _llvmBuilder.BuildBr(mergeBlock);

        // Else
        _llvmBuilder.PositionAtEnd(elseBlock);
        _llvmBuilder.BuildBr(mergeBlock);

        // Merge
        _llvmBuilder.PositionAtEnd(mergeBlock);
        var phi = _llvmBuilder.BuildPhi(LLVMTypeRef.Int1, "and.result");
        phi.AddIncoming([right], [thenBlock], 1);
        phi.AddIncoming([LlvmUtils.CreateConstBool(false)], [elseBlock], 1);

        return phi;
    }

    public LLVMValueRef BuildLogicalOr(LLVMValueRef left, Func<LLVMValueRef> getRight)
    {
        var function = _llvmBuilder.InsertBlock.Parent;
        var thenBlock = function.AppendBasicBlock("or.then");
        var elseBlock = function.AppendBasicBlock("or.else");
        var mergeBlock = function.AppendBasicBlock("or.merge");

        _llvmBuilder.BuildCondBr(left, thenBlock, elseBlock);

        // Then
        _llvmBuilder.PositionAtEnd(thenBlock);
        _llvmBuilder.BuildBr(mergeBlock);

        // Else
        _llvmBuilder.PositionAtEnd(elseBlock);
        var right = getRight();
        _llvmBuilder.BuildBr(mergeBlock);

        // Merge
        _llvmBuilder.PositionAtEnd(mergeBlock);
        var phi = _llvmBuilder.BuildPhi(LLVMTypeRef.Int1, "or.result");
        phi.AddIncoming([LlvmUtils.CreateConstBool(true)], [thenBlock], 1);
        phi.AddIncoming([right], [elseBlock], 1);

        return phi;
    }

    public LLVMValueRef BuildDefaultValueForType(IDataType dataType)
    {
        var type = _llvmTypeBuilder.BuildType(dataType);

        return dataType switch
        {
            PrimitiveDataType d when d.IsInteger() => LLVMValueRef.CreateConstInt(type, 0, true),
            PrimitiveDataType d when d.IsFloat() => LLVMValueRef.CreateConstReal(type, 0),
            PrimitiveDataType { Kind: Primitive.Bool } => LlvmUtils.CreateConstBool(false),
            PrimitiveDataType or FunctionDataType or StructureDataType => LLVMValueRef.CreateConstNull(type),
            _ => throw new NotImplementedException(),
        };
    }

    public LLVMValueRef BuildMalloc(LLVMTypeRef type)
    {
        var sizeType = _llvmTypeBuilder.BuildType(new PrimitiveDataType(Primitive.Int64));
        LLVMTypeRef mallocType;
        mallocType = LLVMTypeRef.CreateFunction(
            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            [sizeType]
        );

        var mallocValue = _llvmModule.AddFunction("malloc", mallocType);
        var call = _llvmBuilder.BuildCall2(
            mallocType,
            mallocValue,
            new LLVMValueRef[] { type.SizeOf },
            "malloc"
        );

        return _llvmBuilder.BuildBitCast(call, LLVMTypeRef.CreatePointer(mallocType, 0), "cast");
    }
}
