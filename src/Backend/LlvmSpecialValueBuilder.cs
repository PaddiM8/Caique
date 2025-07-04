using System.Diagnostics;
using Caique.Analysis;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmSpecialValueBuilder(LlvmEmitterContext emitterContext, LlvmTypeBuilder llvmTypeBuilder)
{
    private readonly LLVMContextRef _context = emitterContext.LlvmContext;
    private readonly LLVMModuleRef _module = emitterContext.LlvmModule;
    private readonly LLVMBuilderRef _builder = emitterContext.LlvmBuilder;
    private readonly LlvmTypeBuilder _typeBuilder = llvmTypeBuilder;

    public LLVMValueRef BuildLogicalAnd(LLVMValueRef left, Func<LLVMValueRef> getRight)
    {
        var function = _builder.InsertBlock.Parent;
        var thenBlock = function.AppendBasicBlock("and.then");
        var elseBlock = function.AppendBasicBlock("and.else");
        var mergeBlock = function.AppendBasicBlock("and.merge");

        _builder.BuildCondBr(left, thenBlock, elseBlock);

        // Then
        _builder.PositionAtEnd(thenBlock);
        var right = getRight();
        _builder.BuildBr(mergeBlock);

        // Else
        _builder.PositionAtEnd(elseBlock);
        _builder.BuildBr(mergeBlock);

        // Merge
        _builder.PositionAtEnd(mergeBlock);
        var phi = _builder.BuildPhi(LLVMTypeRef.Int1, "and.result");
        phi.AddIncoming([right], [thenBlock], 1);
        phi.AddIncoming([LlvmUtils.CreateConstBool(false)], [elseBlock], 1);

        return phi;
    }

    public LLVMValueRef BuildLogicalOr(LLVMValueRef left, Func<LLVMValueRef> getRight)
    {
        var function = _builder.InsertBlock.Parent;
        var thenBlock = function.AppendBasicBlock("or.then");
        var elseBlock = function.AppendBasicBlock("or.else");
        var mergeBlock = function.AppendBasicBlock("or.merge");

        _builder.BuildCondBr(left, thenBlock, elseBlock);

        // Then
        _builder.PositionAtEnd(thenBlock);
        _builder.BuildBr(mergeBlock);

        // Else
        _builder.PositionAtEnd(elseBlock);
        var right = getRight();
        _builder.BuildBr(mergeBlock);

        // Merge
        _builder.PositionAtEnd(mergeBlock);
        var phi = _builder.BuildPhi(LLVMTypeRef.Int1, "or.result");
        phi.AddIncoming([LlvmUtils.CreateConstBool(true)], [thenBlock], 1);
        phi.AddIncoming([right], [elseBlock], 1);

        return phi;
    }

    public LLVMValueRef BuildDefaultValueForType(IDataType dataType)
    {
        var type = _typeBuilder.BuildType(dataType);

        return dataType switch
        {
            PrimitiveDataType d when d.IsInteger() => LLVMValueRef.CreateConstInt(type, 0, true),
            PrimitiveDataType d when d.IsFloat() => LLVMValueRef.CreateConstReal(type, 0),
            PrimitiveDataType { Kind: Primitive.Bool } => LlvmUtils.CreateConstBool(false),
            PrimitiveDataType or SliceDataType or FunctionDataType or StructureDataType => LLVMValueRef.CreateConstNull(type),
            _ => throw new NotImplementedException(),
        };
    }

    public LLVMValueRef BuildMalloc(LLVMTypeRef type, string label = "malloc")
    {
        var sizeType = _typeBuilder.BuildType(new PrimitiveDataType(Primitive.Int64));
        LLVMTypeRef mallocType = LLVMTypeRef.CreateFunction(
            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            [sizeType]
        );

        LLVMValueRef mallocValue = _module.GetNamedFunction("malloc");
        if (mallocValue.Handle == IntPtr.Zero)
            mallocValue = _module.AddFunction("malloc", mallocType);

        var call = _builder.BuildCall2(
            mallocType,
            mallocValue,
            new LLVMValueRef[] { LlvmUtils.BuildSizeOf(_module, type) },
            label
        );

        return _builder.BuildBitCast(call, LLVMTypeRef.CreatePointer(mallocType, 0), "cast");
    }
}
