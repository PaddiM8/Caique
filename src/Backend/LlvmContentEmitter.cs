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
    private readonly LLVMContextRef _context;
    private readonly LLVMBuilderRef _builder;
    private readonly LLVMModuleRef _module;
    private readonly LlvmTypeBuilder _typeBuilder;
    private readonly LlvmContextCache _contextCache;
    private readonly LlvmModuleCache _moduleCache;
    private readonly LlvmSpecialValueBuilder _specialValueBuilder;

    private LlvmContentEmitter(SemanticTree semanticTree, LlvmEmitterContext emitterContext)
    {
        _semanticTree = semanticTree;
        _context = emitterContext.LlvmContext;
        _builder = emitterContext.LlvmBuilder;
        _module = emitterContext.LlvmModule;
        _typeBuilder = emitterContext.LlvmTypeBuilder;
        _contextCache = emitterContext.ContextCache;
        _moduleCache = emitterContext.ModuleCache;
        _specialValueBuilder = new LlvmSpecialValueBuilder(emitterContext, _typeBuilder);
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
            emitter._module.PrintToFile(dumpFilePath);
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

        var success = targetMachine.TryEmitToFile(_module, path, LLVMCodeGenFileType.LLVMObjectFile, out var error);
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
            SemanticKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            SemanticBlockNode blockNode => Visit(blockNode),
            SemanticVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SemanticFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SemanticClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef GetSelf()
    {
        var function = _builder.InsertBlock.Parent;

        return function.GetParam(0);
    }

    private LLVMValueRef Visit(SemanticLiteralNode node)
    {
        var primitive = (PrimitiveDataType)node.DataType;
        return primitive.Kind switch
        {
            Primitive.Void => throw new InvalidOperationException(),
            Primitive.Bool => LlvmUtils.CreateConstBool(node.Value.Kind == TokenKind.True),
            Primitive.String => _builder.BuildGlobalStringPtr(node.Value.Value),
            >= Primitive.Int8 and <= Primitive.Int128 => LLVMValueRef.CreateConstInt(
                _typeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value.Value),
                SignExtend: true
            ),
            >= Primitive.Float16 and <= Primitive.Float64 => LLVMValueRef.CreateConstReal(
                _typeBuilder.BuildType(node.DataType),
                double.Parse(node.Value.Value)
            ),
        };
    }

    private LLVMValueRef Visit(SemanticVariableReferenceNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
        var pointer = _moduleCache.GetNodeLlvmValue((SemanticNode)node.Symbol.SemanticDeclaration);

        return _builder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private LLVMValueRef Visit(SemanticFunctionReferenceNode node)
    {
        Debug.Assert(node.Symbol.SemanticDeclaration != null);

        var functionIdentifier = _contextCache.GetSymbolName(node.Symbol.SemanticDeclaration);
        var functionDeclaration = _module.GetNamedFunction(functionIdentifier);
        if (string.IsNullOrEmpty(functionDeclaration.Name))
        {
            var functionType = _typeBuilder.BuildType(new FunctionDataType(node.Symbol));

            return _module.AddFunction(functionIdentifier, functionType);
        }

        return functionDeclaration;
    }

    private LLVMValueRef Visit(SemanticFieldReferenceNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
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
        var pointer = _builder.BuildStructGEP2(type, objectInstance!.Value, index, $"{node.Identifier.Value}_pointer");

        return _builder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private LLVMValueRef Visit(SemanticUnaryNode node)
    {
        var value = Next(node.Value);
        Debug.Assert(value.HasValue);

        if (node.Operator == TokenKind.Exclamation)
        {
            return _builder.BuildXor(value!.Value, LlvmUtils.CreateConstBool(false), "not");
        }

        if (node.Operator == TokenKind.Minus)
        {
            return node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildNeg(value.Value, "neg"),
                var n when n.IsFloat() => _builder.BuildFNeg(value.Value, "neg"),
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
            return _specialValueBuilder.BuildLogicalAnd(left.Value, () => Next(node.Right)!.Value);

        if (node.Operator == TokenKind.PipePipe)
            return _specialValueBuilder.BuildLogicalOr(left.Value, () => Next(node.Right)!.Value);

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

                return _builder.BuildICmp(predicate, left.Value, right.Value, "cmp");
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

                return _builder.BuildFCmp(predicate, left.Value, right.Value, "cmpf");
            }
        }

        return node.Operator switch
        {
            TokenKind.Plus => node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildAdd(left.Value, right.Value, "add"),
                var n when n.IsFloat() => _builder.BuildFAdd(left.Value, right.Value, "addf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Minus => node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildSub(left.Value, right.Value, "sub"),
                var n when n.IsFloat() => _builder.BuildFSub(left.Value, right.Value, "subf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Star => node.DataType switch
            {
                var n when n.IsInteger() => _builder.BuildMul(left.Value, right.Value, "mul"),
                var n when n.IsFloat() => _builder.BuildFMul(left.Value, right.Value, "mulf"),
                _ => throw new InvalidOperationException(),
            },
            TokenKind.Slash => node.DataType switch
            {
                var n when n.IsSignedInteger() => _builder.BuildSDiv(left.Value, right.Value, "div"),
                var n when n.IsUnsignedInteger() => _builder.BuildUDiv(left.Value, right.Value, "div"),
                var n when n.IsFloat() => _builder.BuildFDiv(left.Value, right.Value, "divf"),
                _ => throw new InvalidOperationException(),
            },
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef Visit(SemanticCallNode node)
    {
        var function = Next(node.Left)!.Value;
        var functionType = _typeBuilder.BuildType(node.Left.DataType);
        var arguments = node.Arguments
            .Select(Next)
            .Select(x => x!.Value)
            .ToArray();

        return _builder.BuildCall2(functionType, function, arguments, "call");
    }

    private LLVMValueRef Visit(SemanticNewNode node)
    {
        var returnType = _typeBuilder.BuildType(node.DataType);
        var structure = (ISemanticInstantiableStructureDeclaration)((StructureDataType)node.DataType).Symbol.SemanticDeclaration!;
        var arguments = node.Arguments
            .Select(Next)
            .Select(x => x!.Value)
            .ToArray();

        var initIdentifier = _contextCache.GetSymbolName(structure.Init);
        var functionDeclaration = _module.GetNamedFunction(initIdentifier);
        if (string.IsNullOrEmpty(functionDeclaration.Name))
        {
            var functionType = _typeBuilder.BuildInitType(structure.Init);
            functionDeclaration = _module.AddFunction(initIdentifier, functionType);
        }

        return _builder.BuildCall2(returnType, functionDeclaration, arguments, "init_call");
    }

    private LLVMValueRef Visit(SemanticReturnNode node)
    {
        var returnTypeNode = _semanticTree.GetEnclosingFunction(node)!.ReturnType;
        var returnType = _typeBuilder.BuildType(returnTypeNode);

        if (node.Value == null)
        {
            _builder.BuildRetVoid();

            return LLVMValueRef.CreateConstNull(returnType);
        }

        var value = Next(node.Value);
        _builder.BuildRet(value!.Value);

        return value.Value;
    }

    private LLVMValueRef Visit(SemanticKeywordValueNode node)
    {
        if (node.Keyword.Value == "size_of")
            return _typeBuilder.BuildType(node.Arguments[0].DataType).SizeOf;

        throw new UnreachableException();
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

        var function = _builder.InsertBlock.Parent;
        var value = function.AppendBasicBlock("entry");
        _moduleCache.SetBlockLlvmValue(node, value);
        _builder.PositionAtEnd(value);

        LLVMValueRef? lastValue = null;
        foreach (var expression in node.Expressions)
            lastValue = Next(expression);

        return lastValue;
    }

    private LLVMValueRef Visit(SemanticVariableDeclarationNode node)
    {
        var block = _builder.InsertBlock;
        var firstInstruction = block.FirstInstruction;
        if (firstInstruction != null)
            _builder.PositionBefore(firstInstruction);

        var alloca = _builder.BuildAlloca(
            _typeBuilder.BuildType(node.DataType),
            node.Identifier.Value
        );

        _builder.PositionAtEnd(block);

        var value = Next(node.Value);
        _builder.BuildStore(value!.Value, alloca);

        return value.Value;
    }

    private LLVMValueRef? Visit(SemanticFunctionDeclarationNode node)
    {
        var function = _moduleCache.GetNodeLlvmValue(node);
        if (node.Body == null)
            return function;

        var block = function.AppendBasicBlock("entry");
        _moduleCache.SetBlockLlvmValue(node.Body, block);
        _builder.PositionAtEnd(block);

        // Allocate parameters
        foreach (var parameter in node.Parameters)
        {
            var parameterType = _typeBuilder.BuildType(parameter.DataType);
            var parameterPointer = _builder.BuildAlloca(parameterType, $"{parameter.Identifier.Value}.addr");
            _moduleCache.SetNodeLlvmValue(parameter, parameterPointer);
        }

        // Store parameters
        var parameterOffset = node.IsStatic ? (uint)0 : 1;
        foreach (var (i, parameter) in node.Parameters.Index())
        {
            var parameterValue = function.GetParam((uint)i + parameterOffset);
            var parameterPointer = _moduleCache.GetNodeLlvmValue(parameter);
            _builder.BuildStore(parameterValue, parameterPointer);
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
            _builder.BuildRetVoid();
        }
        else if (node.ReturnType is PrimitiveDataType { Kind: Primitive.Void })
        {
            Next(last);
            _builder.BuildRetVoid();
        }
        else
        {
            var lastValue = Next(last);
            if (lastValue.HasValue)
                _builder.BuildRet(lastValue.Value);
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
            var entryFunction = _module.AddFunction("main", entryFunctionType);
            var block = entryFunction.AppendBasicBlock("entry");
            _builder.PositionAtEnd(block);

            // Find the user-defined main function
            var userEntryDeclaration = node.Functions.FirstOrDefault(x => x.Identifier.Value == "Main");
            if (userEntryDeclaration == null)
                return null;

            // Build a call to the user-defined main function
            var userMainFunction = _moduleCache.GetNodeLlvmValue(userEntryDeclaration);
            var userMainFunctionType = _typeBuilder.BuildType(new FunctionDataType(userEntryDeclaration.Symbol));
            var returnValue = _builder.BuildCall2(
                userMainFunctionType,
                userMainFunction,
                Array.Empty<LLVMValueRef>(),
                "main"
            );
            _builder.BuildRet(returnValue);
        }

        return null;
    }

    private LLVMValueRef Visit(SemanticInitNode node, SemanticClassDeclarationNode parentStructure)
    {
        var function = _moduleCache.GetNodeLlvmValue(node);
        var value = function.AppendBasicBlock("entry");
        _moduleCache.SetBlockLlvmValue(node.Body, value);
        _builder.PositionAtEnd(value);

        var type = _typeBuilder.BuildNamedStructType(new StructureDataType(parentStructure.Symbol));
        var instance = _specialValueBuilder.BuildMalloc(type);

        foreach (var expression in node.Body.Expressions)
            Next(expression);

        foreach (var (i, field) in parentStructure.Fields.Index())
        {
            var index = (uint)parentStructure.FieldStartIndex + (uint)i;
            var fieldType = _typeBuilder.BuildType(field.DataType);
            var fieldPointer = _builder.BuildStructGEP2(fieldType, instance, index, field.Identifier.Value);
            var fieldValue = field.Value == null
                ? _specialValueBuilder.BuildDefaultValueForType(field.DataType)
                : Next(field.Value);
            _builder.BuildStore(fieldValue!.Value, fieldPointer);
        }

        _builder.BuildRet(instance);

        return function;
    }
}
