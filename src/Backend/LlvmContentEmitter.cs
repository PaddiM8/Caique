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
    private readonly NamespaceScope _preludeScope;
    private readonly LlvmSpecialValueBuilder _specialValueBuilder;

    private LlvmContentEmitter(SemanticTree semanticTree, LlvmEmitterContext emitterContext, CompilationContext compilationContext)
    {
        _semanticTree = semanticTree;
        _context = emitterContext.LlvmContext;
        _builder = emitterContext.LlvmBuilder;
        _module = emitterContext.LlvmModule;
        _typeBuilder = emitterContext.LlvmTypeBuilder;
        _contextCache = emitterContext.ContextCache;
        _moduleCache = emitterContext.ModuleCache;
        _preludeScope = compilationContext.PreludeScope;
        _specialValueBuilder = new LlvmSpecialValueBuilder(emitterContext, _typeBuilder);
    }

    public static string Emit(
        SemanticTree semanticTree,
        LlvmEmitterContext emitterContext,
        string targetDirectory,
        CompilationContext compilationContext,
        CompilationOptions? compilationOptions = null
    )
    {
        var emitter = new LlvmContentEmitter(semanticTree, emitterContext, compilationContext);
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
        Debug.Assert(node.Parent != null || node == _semanticTree.Root);

        return node switch
        {
            SemanticLiteralNode literalNode => Visit(literalNode),
            SemanticVariableReferenceNode variableReferenceNode => Visit(variableReferenceNode),
            SemanticFunctionReferenceNode functionReferenceNode => Visit(functionReferenceNode),
            SemanticFieldReferenceNode fieldReferenceNode => Visit(fieldReferenceNode),
            SemanticUnaryNode unaryNode => Visit(unaryNode),
            SemanticBinaryNode binaryNode => Visit(binaryNode),
            SemanticAssignmentNode assignmentNode => Visit(assignmentNode),
            SemanticCallNode callNode => Visit(callNode),
            SemanticNewNode newNode => Visit(newNode),
            SemanticReturnNode returnNode => Visit(returnNode),
            SemanticKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            SemanticCastNode castNode => Visit(castNode),
            SemanticBlockNode blockNode => Visit(blockNode),
            SemanticVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            SemanticFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            SemanticClassDeclarationNode classDeclarationNode => Visit(classDeclarationNode),
            _ => throw new NotImplementedException(),
        };
    }

    private LLVMValueRef GetSelf()
    {
        return _builder.InsertBlock.Parent.GetParam(0);
    }

    private LLVMValueRef BuildString(string value)
    {
        var valuePointer = _builder.BuildGlobalStringPtr(value);
        var symbol = _preludeScope.ResolveStructure(["String"])!;
        var type = _typeBuilder.BuildNamedStructType(new StructureDataType(symbol.SemanticDeclaration!.Symbol));
        var instance = _specialValueBuilder.BuildMalloc(type, "str");
        var structure = (ISemanticInstantiableStructureDeclaration)symbol.SemanticDeclaration;
        BuildConstructorCall(instance, structure, [valuePointer]);

        return instance;
    }

    private LLVMValueRef Visit(SemanticLiteralNode node)
    {
        if (node.DataType is StructureDataType { Symbol.Name: "String" })
            return BuildString(node.Value.Value);

        var primitive = (PrimitiveDataType)node.DataType;
        return primitive.Kind switch
        {
            Primitive.Void => throw new InvalidOperationException(),
            Primitive.Bool => LlvmUtils.CreateConstBool(node.Value.Kind == TokenKind.True),
            >= Primitive.Int8 and <= Primitive.Int128 => LLVMValueRef.CreateConstInt(
                _typeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value.Value),
                SignExtend: true
            ),
            >= Primitive.Uint8 and <= Primitive.Uint128 => LLVMValueRef.CreateConstInt(
                _typeBuilder.BuildType(node.DataType),
                ulong.Parse(node.Value.Value),
                SignExtend: false
            ),
            >= Primitive.Float16 and <= Primitive.Float64 => LLVMValueRef.CreateConstReal(
                _typeBuilder.BuildType(node.DataType),
                double.Parse(node.Value.Value)
            ),
        };
    }

    private LLVMValueRef Visit(SemanticVariableReferenceNode node)
    {
        var (type, pointer) = ResolveVariableReference(node);

        return _builder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private (LLVMTypeRef type, LLVMValueRef pointer) ResolveVariableReference(SemanticVariableReferenceNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
        var pointer = _moduleCache.GetNodeLlvmValue((SemanticNode)node.Symbol.SemanticDeclaration);

        return (type, pointer);
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
        var (type, pointer) = ResolveFieldReference(node);

        return _builder.BuildLoad2(type, pointer, node.Identifier.Value);
    }

    private (LLVMTypeRef type, LLVMValueRef pointer) ResolveFieldReference(SemanticFieldReferenceNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
        if (node.ObjectInstance == null)
        {
            throw new NotImplementedException("Static fields have not been implemented yet.");
        }

        var structure = _semanticTree.GetEnclosingStructure(node)!;
        var instanceDataType = (StructureDataType)node.ObjectInstance.DataType;
        var instanceLlvmType = _typeBuilder.BuildNamedStructType(instanceDataType);
        var instance = node.ObjectInstance == null
            ? GetSelf()
            : Next(node.ObjectInstance);

        Debug.Assert(node.Symbol.SemanticDeclaration != null);
        var index = (uint)structure.FieldStartIndex + (uint)structure.Fields.IndexOf(node.Symbol.SemanticDeclaration);
        var pointer = _builder.BuildStructGEP2(instanceLlvmType, instance!.Value, index, $"{node.Identifier.Value}_pointer");

        return (type, pointer);
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

    private LLVMValueRef Visit(SemanticAssignmentNode node)
    {
        LLVMValueRef pointer;
        if (node.Left is SemanticVariableReferenceNode variableReferenceNode)
        {
            pointer = ResolveVariableReference(variableReferenceNode).pointer;
        }
        else if (node.Left is SemanticFieldReferenceNode fieldReferenceNode)
        {
            pointer = ResolveFieldReference(fieldReferenceNode).pointer;
        }
        else
        {
            throw new NotImplementedException();
        }

        var value = Next(node.Right)!.Value;
        _builder.BuildStore(value!, pointer);

        return value;
    }

    private LLVMValueRef Visit(SemanticCallNode node)
    {
        var function = Next(node.Left)!.Value;
        var functionType = _typeBuilder.BuildType(node.Left.DataType);
        var arguments = node.Arguments
            .Select(Next)
            .Select(x => x!.Value)
            .ToList();

        if (node.Left is SemanticFunctionReferenceNode { ObjectInstance: not null } functionReference)
        {
            var instance = Next(functionReference.ObjectInstance);
            arguments.Insert(0, instance!.Value);
        }

        var name = node.DataType.IsVoid()
            ? string.Empty
            : "call";

        return _builder.BuildCall2(functionType, function, arguments.ToArray(), name);
    }

    private LLVMValueRef Visit(SemanticNewNode node)
    {
        var type = _typeBuilder.BuildType(node.DataType);
        var structure = (ISemanticInstantiableStructureDeclaration)((StructureDataType)node.DataType).Symbol.SemanticDeclaration!;
        var instance = _specialValueBuilder.BuildMalloc(type, "self");
        var arguments = node
            .Arguments
            .Select(Next)
            .Select(x => x!.Value);
        BuildConstructorCall(instance, structure, arguments);

        return instance;
    }

    private void BuildConstructorCall(
        LLVMValueRef instance,
        ISemanticInstantiableStructureDeclaration structure,
        IEnumerable<LLVMValueRef> arguments
    )
    {
        var builtArguments = arguments
            .Prepend(instance)
            .ToArray();

        var initIdentifier = _contextCache.GetSymbolName(structure.Init);
        var functionDeclaration = _module.GetNamedFunction(initIdentifier);
        var functionType = _typeBuilder.BuildInitType(structure.Init);
        if (string.IsNullOrEmpty(functionDeclaration.Name))
            functionDeclaration = _module.AddFunction(initIdentifier, functionType);

        _builder.BuildCall2(functionType, functionDeclaration, builtArguments);
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
            return _typeBuilder.BuildType(node.Arguments![0].DataType).SizeOf;

        if (node.Keyword.Kind is TokenKind.Self or TokenKind.Base)
            return GetSelf();

        throw new UnreachableException();
    }

    private LLVMValueRef Visit(SemanticCastNode node)
    {
        var value = Next(node.Value)!.Value;
        if (node.DataType is PrimitiveDataType primitiveDataType)
            return BuildPrimitiveCast(value, node.Value.DataType, primitiveDataType);

        if (node.DataType is StructureDataType structureDataType)
        {
            var toLlvmType = _typeBuilder.BuildNamedStructType(structureDataType);

            return _builder.BuildBitCast(value, toLlvmType, "bitcast");
        }

        throw new NotImplementedException();
    }

    private LLVMValueRef BuildPrimitiveCast(
        LLVMValueRef value,
        IDataType fromDataType,
        PrimitiveDataType toPrimitive
    )
    {
        if (fromDataType is not PrimitiveDataType fromPrimitive)
            throw new NotImplementedException();

        bool isUpcast = toPrimitive.BitSize > fromPrimitive.BitSize;
        bool isDowncast = toPrimitive.BitSize < fromPrimitive.BitSize;
        var toLlvmType = _typeBuilder.BuildType(toPrimitive);

        LLVMOpcode? opCode;
        if (toPrimitive.IsInteger())
        {
            opCode = fromPrimitive switch
            {
                var from when from.IsSignedInteger() && isUpcast => LLVMOpcode.LLVMZExt,
                var from when from.IsUnsignedInteger() && isUpcast => LLVMOpcode.LLVMSExt,
                var from when from.IsInteger() && isDowncast => LLVMOpcode.LLVMTrunc,
                var from when from.IsFloat() && toPrimitive.IsSignedInteger() => LLVMOpcode.LLVMFPToSI,
                var from when from.IsFloat() && toPrimitive.IsUnsignedInteger() => LLVMOpcode.LLVMFPToUI,
                _ => null,
            };
        }
        else if (toPrimitive.IsFloat())
        {
            opCode = fromPrimitive switch
            {
                var from when from.IsSignedInteger() => LLVMOpcode.LLVMSIToFP,
                var from when from.IsUnsignedInteger() => LLVMOpcode.LLVMUIToFP,
                var from when from.IsFloat() && isUpcast => LLVMOpcode.LLVMFPExt,
                var from when from.IsFloat() && isDowncast => LLVMOpcode.LLVMFPTrunc,
                _ => null,
            };
        }
        else
        {
            throw new NotImplementedException();
        }

        if (!opCode.HasValue)
            return value;

        return _builder.BuildCast(opCode.Value, value, toLlvmType, "cast");
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
        _moduleCache.SetNodeLlvmValue(node, alloca);

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
            var returnsVoid = userEntryDeclaration.ReturnType is PrimitiveDataType { Kind: Primitive.Void };
            var returnValue = _builder.BuildCall2(
                userMainFunctionType,
                userMainFunction,
                Array.Empty<LLVMValueRef>(),
                returnsVoid ? string.Empty : "main"
            );

            if (returnsVoid)
            {
                _builder.BuildRet(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false));
            }
            else
            {
                _builder.BuildRet(returnValue);
            }
        }

        return null;
    }

    private LLVMValueRef Visit(SemanticInitNode node, SemanticClassDeclarationNode parentStructure)
    {
        var function = _moduleCache.GetNodeLlvmValue(node);
        var value = function.AppendBasicBlock("entry");
        _moduleCache.SetBlockLlvmValue(node.Body, value);
        _builder.PositionAtEnd(value);

        // Allocate parameters
        foreach (var parameter in node.Parameters)
        {
            var parameterType = _typeBuilder.BuildType(parameter.DataType);
            var parameterPointer = _builder.BuildAlloca(parameterType, $"{parameter.Identifier.Value}.addr");
            _moduleCache.SetNodeLlvmValue(parameter, parameterPointer);
        }

        // Store parameters
        uint parameterOffset = 1;
        foreach (var (i, parameter) in node.Parameters.Index())
        {
            var parameterValue = function.GetParam((uint)i + parameterOffset);
            var parameterPointer = _moduleCache.GetNodeLlvmValue(parameter);
            _builder.BuildStore(parameterValue, parameterPointer);
        }

        // Insert default values
        var instance = GetSelf();
        var type = _typeBuilder.BuildNamedStructType(new StructureDataType(parentStructure.Symbol));
        foreach (var (i, field) in parentStructure.GetAllFields().Index())
        {
            var fieldPointer = _builder.BuildStructGEP2(type, instance, (uint)i, field.Identifier.Value);
            var fieldValue = field.Value == null
                ? _specialValueBuilder.BuildDefaultValueForType(field.DataType)
                : Next(field.Value);
            _builder.BuildStore(fieldValue!.Value, fieldPointer);
        }

        // Call base constructor (if any)
        if (parentStructure.InheritedClass != null)
        {
            IEnumerable<LLVMValueRef> baseCallArguments = [];
            if (node.BaseCall != null)
            {
                baseCallArguments = node
                    .BaseCall
                    .Arguments!
                    .Select(Next)
                    .Select(x => x!.Value);
            }

            BuildConstructorCall(
                instance,
                (SemanticClassDeclarationNode)parentStructure.InheritedClass.SemanticDeclaration!,
                baseCallArguments
            );
        }

        foreach (var expression in node.Body.Expressions)
            Next(expression);

        _builder.BuildRetVoid();

        return function;
    }
}
