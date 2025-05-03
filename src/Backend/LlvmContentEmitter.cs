using System.Diagnostics;
using System.Runtime.InteropServices;
using Caique.Analysis;
using Caique.Lexing;
using Caique.Parsing;
using Caique.Scope;
using LLVMSharp.Interop;

namespace Caique.Backend;

public class LlvmContentEmitter
{
    private readonly SemanticTree _semanticTree;
    private readonly LLVMContextRef _llvmContext;
    private readonly LLVMBuilderRef _llvmBuilder;
    private readonly LLVMModuleRef _llvmModule;
    private readonly LlvmTypeBuilder _llvmTypeBuilder;
    private readonly LlvmCache _globalCache;
    private readonly LlvmCache _localCache;
    private readonly LlvmSpecialValueBuilder _llvmSpecialValueBuilder;

    private LlvmContentEmitter(SemanticTree semanticTree, LlvmEmitterContext emitterContext)
    {
        _semanticTree = semanticTree;
        _llvmContext = emitterContext.LlvmContext;
        _llvmBuilder = emitterContext.LlvmBuilder;
        _llvmModule = emitterContext.LlvmModule;
        _llvmTypeBuilder = emitterContext.LlvmTypeBuilder;
        _globalCache = emitterContext.GlobalCache;
        _localCache = new LlvmCache();
        _llvmSpecialValueBuilder = new LlvmSpecialValueBuilder(emitterContext, _llvmTypeBuilder);
    }

    public static string Emit(
        SemanticTree semanticTree,
        LlvmEmitterContext emitterContext,
        string targetDirectory,
        CompilationOptions? compilationOptions = null
    )
    {
        var emitter = new LlvmContentEmitter(semanticTree, emitterContext);
        emitter.Next(semanticTree.Root);

        if (compilationOptions?.DumpIr is true)
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, "ir"));
            var dumpFilePath = Path.Combine(targetDirectory, "ir", $"{emitterContext.ModuleName}.ll");
            emitter._llvmModule.PrintToFile(dumpFilePath);
        }

        Directory.CreateDirectory(Path.Combine(targetDirectory, "objects"));
        var objectPath = Path.Combine(targetDirectory, "objects", $"{emitterContext.ModuleName}.o");
        emitter.CreateObjectFile(objectPath);

        return objectPath;
    }

    private void CreateObjectFile(string path)
    {
        LLVM.InitializeNativeTarget();
        LLVM.InitializeNativeAsmPrinter();

        var target = LLVMTargetRef.GetTargetFromTriple(LLVMTargetRef.DefaultTriple);
        string hostCpu;
        string hostCpuFeatures;
        unsafe
        {
            hostCpu = Marshal.PtrToStringAnsi((IntPtr)LLVM.GetHostCPUName())!;
            hostCpuFeatures = Marshal.PtrToStringAnsi((IntPtr)LLVM.GetHostCPUFeatures())!;
        }

        var targetMachine = target.CreateTargetMachine(
            LLVMTargetRef.DefaultTriple,
            hostCpu,
            hostCpuFeatures,
            LLVMCodeGenOptLevel.LLVMCodeGenLevelNone,
            LLVMRelocMode.LLVMRelocDefault,
            LLVMCodeModel.LLVMCodeModelDefault
        );

        var success = targetMachine.TryEmitToFile(_llvmModule, path, LLVMCodeGenFileType.LLVMObjectFile, out var error);
        if (!success)
            throw new Exception(error);
    }

    private LLVMValueRef? Next(SemanticNode node)
    {
        return node switch
        {
            SemanticLiteralNode literalNode => Visit(literalNode),
            SemanticVariableReferenceNode variableReferenceNode => Visit(variableReferenceNode),
            SemanticFunctionReferenceNode functionReferenceNode => Visit(functionReferenceNode),
            SemanticFieldReferenceNode fieldReferenceNode => Visit(fieldReferenceNode),
            SemanticUnaryNode unaryNode => Visit(unaryNode),
            SemanticBinaryNode binaryNode => Visit(binaryNode),
            SemanticCallNode callNode => Visit(callNode),
            SemanticNewNode newNode => Visit(newNode),
            SemanticReturnNode returnNode => Visit(returnNode),
            SemanticBlockNode blockNode => Visit(blockNode),
            SemanticVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SemanticFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SemanticClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef GetSelf()
    {
        var function = _llvmBuilder.InsertBlock.Parent;

        return function.GetParam(0);
    }

    private LLVMValueRef Visit(SemanticLiteralNode node)
    {
        var primitive = (PrimitiveDataType)node.DataType;
        return primitive.Kind switch
        {
            Primitive.Void => throw new InvalidOperationException(),
            Primitive.Bool => LlvmUtils.CreateConstBool(node.Value.Kind == TokenKind.True),
            >= Primitive.Int8 and <= Primitive.Int128 => LLVMValueRef.CreateConstInt(
                _llvmTypeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value.Value),
                SignExtend: true
            ),
            >= Primitive.Float16 and <= Primitive.Float64 => LLVMValueRef.CreateConstReal(
                _llvmTypeBuilder.BuildType(node.DataType),
                double.Parse(node.Value.Value)
            ),
        };
    }

    private LLVMValueRef Visit(SemanticVariableReferenceNode node)
    {
        var type = _llvmTypeBuilder.BuildType(node.DataType);
        var pointer = _localCache.GetNodeLlvmValue((SemanticNode)node.Symbol.SemanticDeclaration);

        return _llvmBuilder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private LLVMValueRef Visit(SemanticFunctionReferenceNode node)
    {
        Debug.Assert(node.Symbol.SemanticDeclaration != null);

        var functionDefinition = _globalCache.GetNodeLlvmValue(node.Symbol.SemanticDeclaration);
        var functionDeclaration = _llvmModule.GetNamedFunction(functionDefinition.Name);
        if (string.IsNullOrEmpty(functionDeclaration.Name))
        {
            var functionType = _llvmTypeBuilder.BuildType(new FunctionDataType(node.Symbol));

            return _llvmModule.AddFunction(functionDefinition.Name, functionType);
        }

        return functionDeclaration;
    }

    private LLVMValueRef Visit(SemanticFieldReferenceNode node)
    {
        var type = _llvmTypeBuilder.BuildType(node.DataType);
        if (node.IsStatic)
        {
            throw new NotImplementedException();
        }

        var objectInstance = node.ExplicitObjectInstance == null
            ? GetSelf()
            : Next(node.ExplicitObjectInstance);
        var structure = _semanticTree.GetEnclosingStructure(node);

        Debug.Assert(node.Symbol.SemanticDeclaration != null);
        var index = (uint)structure!.FieldStartIndex + (uint)structure.Fields.IndexOf(node.Symbol.SemanticDeclaration);
        var pointer = _llvmBuilder.BuildStructGEP2(type, objectInstance!.Value, index, $"{node.Identifier.Value}_pointer");

        return _llvmBuilder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private LLVMValueRef Visit(SemanticUnaryNode node)
    {
        var value = Next(node.Value);
        Debug.Assert(value.HasValue);

        if (node.Operator == TokenKind.Exclamation)
        {
            return _llvmBuilder.BuildXor(value!.Value, LlvmUtils.CreateConstBool(false), "not");
        }

        if (node.Operator == TokenKind.Minus)
        {
            return node.DataType switch
            {
                var n when n.IsInteger() => _llvmBuilder.BuildNeg(value.Value, "neg"),
                var n when n.IsFloat() => _llvmBuilder.BuildFNeg(value.Value, "neg"),
                _ => throw new InvalidOperationException(),
            };
        }

        throw new NotImplementedException();
    }

    private LLVMValueRef Visit(SemanticBinaryNode node)
    {
        var left = Next(node.Left);
        Debug.Assert(left.HasValue);

        if (node.Operator == TokenKind.AmpersandAmpersand)
            return _llvmSpecialValueBuilder.BuildLogicalAnd(left.Value, () => Next(node.Right)!.Value);

        if (node.Operator == TokenKind.PipePipe)
            return _llvmSpecialValueBuilder.BuildLogicalOr(left.Value, () => Next(node.Right)!.Value);

        var right = Next(node.Right);
        Debug.Assert(right.HasValue);

        if (node.Operator is
            TokenKind.EqualsEquals or
            TokenKind.NotEquals or
            TokenKind.Greater or
            TokenKind.GreaterEquals or
            TokenKind.Less or
            TokenKind.LessEquals
        )
        {
            if (node.DataType.IsInteger())
            {
                var isSigned = node.DataType.IsSignedInteger();
                var predicate = node.Operator switch
                {
                    TokenKind.EqualsEquals => LLVMIntPredicate.LLVMIntEQ,
                    TokenKind.NotEquals => LLVMIntPredicate.LLVMIntNE,
                    TokenKind.Greater => isSigned
                        ? LLVMIntPredicate.LLVMIntSGT
                        : LLVMIntPredicate.LLVMIntUGT,
                    TokenKind.GreaterEquals => isSigned
                        ? LLVMIntPredicate.LLVMIntSGE
                        : LLVMIntPredicate.LLVMIntUGE,
                    TokenKind.Less => isSigned
                        ? LLVMIntPredicate.LLVMIntSLT
                        : LLVMIntPredicate.LLVMIntULT,
                    TokenKind.LessEquals => isSigned
                        ? LLVMIntPredicate.LLVMIntSLT
                        : LLVMIntPredicate.LLVMIntULT,
                    _ => throw new UnreachableException(),
                };

                return _llvmBuilder.BuildICmp(predicate, left.Value, right.Value, "cmp");
            }
            else if (node.DataType.IsFloat())
            {
                var predicate = node.Operator switch
                {
                    TokenKind.EqualsEquals => LLVMRealPredicate.LLVMRealOEQ,
                    TokenKind.NotEquals => LLVMRealPredicate.LLVMRealONE,
                    TokenKind.Greater => LLVMRealPredicate.LLVMRealOGT,
                    TokenKind.GreaterEquals => LLVMRealPredicate.LLVMRealOGE,
                    TokenKind.Less => LLVMRealPredicate.LLVMRealOLT,
                    TokenKind.LessEquals => LLVMRealPredicate.LLVMRealOLE,
                    _ => throw new UnreachableException(),
                };

                return _llvmBuilder.BuildFCmp(predicate, left.Value, right.Value, "cmpf");
            }
        }

        return node.Operator switch
        {
            TokenKind.Plus => node.DataType switch
            {
                var n when n.IsInteger() => _llvmBuilder.BuildAdd(left.Value, right.Value, "add"),
                var n when n.IsFloat() => _llvmBuilder.BuildFAdd(left.Value, right.Value, "addf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Minus => node.DataType switch
            {
                var n when n.IsInteger() => _llvmBuilder.BuildSub(left.Value, right.Value, "sub"),
                var n when n.IsFloat() => _llvmBuilder.BuildFSub(left.Value, right.Value, "subf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Star => node.DataType switch
            {
                var n when n.IsInteger() => _llvmBuilder.BuildMul(left.Value, right.Value, "mul"),
                var n when n.IsFloat() => _llvmBuilder.BuildFMul(left.Value, right.Value, "mulf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Slash => node.DataType switch
            {
                var n when n.IsSignedInteger() => _llvmBuilder.BuildSDiv(left.Value, right.Value, "div"),
                var n when n.IsUnsignedInteger() => _llvmBuilder.BuildUDiv(left.Value, right.Value, "div"),
                var n when n.IsFloat() => _llvmBuilder.BuildFDiv(left.Value, right.Value, "divf"),
                _ => throw new InvalidOperationException(),
            },
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef Visit(SemanticCallNode node)
    {
        var function = Next(node.Left)!.Value;
        var functionType = _llvmTypeBuilder.BuildType(node.Left.DataType);
        var arguments = node.Arguments
            .Select(Next)
            .Select(x => x!.Value)
            .ToArray();

        return _llvmBuilder.BuildCall2(functionType, function, arguments, "call");
    }

    private LLVMValueRef Visit(SemanticNewNode node)
    {
        var returnType = _llvmTypeBuilder.BuildType(node.DataType);
        var structure = (ISemanticInstantiableStructureDeclaration)((StructureDataType)node.DataType).Symbol.SemanticDeclaration!;
        var function = _globalCache.GetNodeLlvmValue(structure.Init);
        var arguments = node.Arguments
            .Select(Next)
            .Select(x => x!.Value)
            .ToArray();

        return _llvmBuilder.BuildCall2(returnType, function, arguments, "init_call");
    }

    private LLVMValueRef Visit(SemanticReturnNode node)
    {
        var returnTypeNode = _semanticTree.GetEnclosingFunction(node)!.ReturnType;
        var returnType = _llvmTypeBuilder.BuildType(returnTypeNode);

        if (node.Value == null)
        {
            _llvmBuilder.BuildRetVoid();

            return LLVMValueRef.CreateConstNull(returnType);
        }

        var value = Next(node.Value);
        _llvmBuilder.BuildRet(value!.Value);

        return value.Value;
    }

    private LLVMValueRef? Visit(SemanticBlockNode node)
    {
        // If it's the root block, we shouldn't create an LLVM basic block.
        // Just emit the children.
        if (node.Parent == null)
        {
            foreach (var expression in node.Expressions)
                Next(expression);

            return null;
        }

        var function = _llvmBuilder.InsertBlock.Parent;
        var value = function.AppendBasicBlock("entry");
        _localCache.SetBlockLlvmValue(node, value);
        _llvmBuilder.PositionAtEnd(value);

        LLVMValueRef? lastValue = null;
        foreach (var expression in node.Expressions)
            lastValue = Next(expression);

        return lastValue;
    }

    private LLVMValueRef Visit(SemanticVariableDeclarationNode node)
    {
        var block = _llvmBuilder.InsertBlock;
        var firstInstruction = block.FirstInstruction;
        if (firstInstruction != null)
            _llvmBuilder.PositionBefore(firstInstruction);

        var alloca = _llvmBuilder.BuildAlloca(
            _llvmTypeBuilder.BuildType(node.DataType),
            node.Identifier.Value
        );

        _llvmBuilder.PositionAtEnd(block);

        var value = Next(node.Value);
        _llvmBuilder.BuildStore(value!.Value, alloca);

        return value.Value;
    }

    private LLVMValueRef? Visit(SemanticFunctionDeclarationNode node)
    {
        var function = _globalCache.GetNodeLlvmValue(node);
        var block = function.AppendBasicBlock("entry");
        _localCache.SetBlockLlvmValue(node.Body, block);
        _llvmBuilder.PositionAtEnd(block);

        // Allocate parameters
        foreach (var parameter in node.Parameters)
        {
            var parameterType = _llvmTypeBuilder.BuildType(parameter.DataType);
            var parameterPointer = _llvmBuilder.BuildAlloca(parameterType, $"{parameter.Identifier.Value}.addr");
            _localCache.SetNodeLlvmValue(parameter, parameterPointer);
        }

        // Store parameters
        var parameterOffset = node.IsStatic ? (uint)0 : 1;
        foreach (var (i, parameter) in node.Parameters.Index())
        {
            var parameterValue = function.GetParam((uint)i + parameterOffset);
            var parameterPointer = _localCache.GetNodeLlvmValue(parameter);
            _llvmBuilder.BuildStore(parameterValue, parameterPointer);
        }

        foreach (var expression in node.Body.Expressions.SkipLast(1))
            Next(expression);

        var last = node.Body.Expressions.LastOrDefault();
        if (last is SemanticReturnNode)
        {
            Next(last);
        }
        else if (last == null)
        {
            _llvmBuilder.BuildRetVoid();
        }
        else if (node.ReturnType is PrimitiveDataType { Kind: Primitive.Void })
        {
            Next(last);
            _llvmBuilder.BuildRetVoid();
        }
        else
        {
            var lastValue = Next(last);
            if (lastValue.HasValue)
                _llvmBuilder.BuildRet(lastValue.Value);
        }

        return function;
    }

    private LLVMValueRef? Visit(SemanticClassDeclarationNode node)
    {
        Visit(node.Init, node);

        foreach (var function in node.Functions)
            Next(function);

        if (node.Identifier.Value == "Program")
        {
            // Create the entry function
            var entryFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, []);
            var entryFunction = _llvmModule.AddFunction("main", entryFunctionType);
            var block = entryFunction.AppendBasicBlock("entry");
            _llvmBuilder.PositionAtEnd(block);

            // Find the user-defined main function
            var userEntryDeclaration = node.Functions.FirstOrDefault(x => x.Identifier.Value == "Main");
            if (userEntryDeclaration == null)
                return null;

            // Build a call to the user-defined main function
            var userMainFunction = _globalCache.GetNodeLlvmValue(userEntryDeclaration);
            var userMainFunctionType = _llvmTypeBuilder.BuildType(new FunctionDataType(userEntryDeclaration.Symbol));
            var returnValue = _llvmBuilder.BuildCall2(
                userMainFunctionType,
                userMainFunction,
                Array.Empty<LLVMValueRef>(),
                "main"
            );
            _llvmBuilder.BuildRet(returnValue);
        }

        return null;
    }

    private LLVMValueRef Visit(SemanticInitNode node, SemanticClassDeclarationNode parentStructure)
    {
        var function = _globalCache.GetNodeLlvmValue(node);
        var value = function.AppendBasicBlock("entry");
        _localCache.SetBlockLlvmValue(node.Body, value);
        _llvmBuilder.PositionAtEnd(value);

        var type = _llvmTypeBuilder.BuildNamedStructType(new StructureDataType(parentStructure.Symbol));
        var instance = _llvmSpecialValueBuilder.BuildMalloc(type);

        foreach (var expression in node.Body.Expressions)
            Next(expression);

        foreach (var (i, field) in parentStructure.Fields.Index())
        {
            var index = (uint)parentStructure.FieldStartIndex + (uint)i;
            var fieldType = _llvmTypeBuilder.BuildType(field.DataType);
            var fieldPointer = _llvmBuilder.BuildStructGEP2(fieldType, instance, index, field.Identifier.Value);
            var fieldValue = field.Value == null
                ? _llvmSpecialValueBuilder.BuildDefaultValueForType(field.DataType)
                : Next(field.Value);
            _llvmBuilder.BuildStore(fieldValue!.Value, fieldPointer);
        }

        _llvmBuilder.BuildRet(instance);

        return function;
    }
}
