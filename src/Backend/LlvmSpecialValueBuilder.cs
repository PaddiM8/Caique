using System.Diagnostics;
using Caique.Analysis;
using Caique.Lowering;
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
        var phi = _builder.BuildPhi(_context.Int1Type, "and.result");
        phi.AddIncoming([right], [thenBlock], 1);
        phi.AddIncoming([LlvmUtils.CreateConstBool(_context, false)], [elseBlock], 1);

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
        var phi = _builder.BuildPhi(_context.Int1Type, "or.result");
        phi.AddIncoming([LlvmUtils.CreateConstBool(_context, true)], [thenBlock], 1);
        phi.AddIncoming([right], [elseBlock], 1);

        return phi;
    }

    public LLVMValueRef BuildDefaultValueForType(ILoweredDataType dataType)
    {
        var type = _typeBuilder.BuildType(dataType);
        var primitive = (dataType as LoweredPrimitiveDataType)?.Primitive;

        return primitive switch
        {
            var d when d?.IsInteger() is true => LLVMValueRef.CreateConstInt(type, 0, true),
            var d when d?.IsFloat() is true => LLVMValueRef.CreateConstReal(type, 0),
            Primitive.Bool => LlvmUtils.CreateConstBool(_context, false),
            _ => LLVMValueRef.CreateConstNull(type),
        };
    }
}
