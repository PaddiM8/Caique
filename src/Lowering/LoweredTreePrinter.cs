using System.Text;
using Caique.Lexing;

namespace Caique.Lowering;

public class LoweredTreePrinter
{
    private readonly StringBuilder _typeBuilder = new();
    private readonly HashSet<string> _generatedComplexTypes = [];
    private int _variableCount = 0;

    public static void PrintToFile(LoweredTree tree, string path)
    {
        var printer = new LoweredTreePrinter();
        var builder = new StringBuilder();
        foreach (var global in tree.Globals)
            builder.AppendLine(printer.Next(global.Value).Item1.ToString());

        foreach (var structure in tree.Structs)
            builder.AppendLine(printer.Next(structure.Value).Item1.ToString());

        foreach (var function in tree.Functions)
            builder.AppendLine(printer.Next(function.Value).Item1.ToString());

        File.WriteAllText(path, printer._typeBuilder.ToString() + builder);
    }

    private string NextDataType(ILoweredDataType dataType)
    {
        var stringValue = dataType.ToString()!;
        if (dataType is LoweredStructDataType structDataType)
        {
            if (_generatedComplexTypes.Contains(stringValue))
                return stringValue;

            var fields = structDataType
                .Fields
                .Where(x => !x.IsPlaceholder)
                .Select(x => $"{x.Name}: {NextDataType(x.DataType)}")
                .ToList();
            _typeBuilder.AppendLine($"type {structDataType.Name} = ");
            _typeBuilder.AppendLine("{");

            if (fields.Count > 0)
            {
                _typeBuilder.Append("    ");
                _typeBuilder.AppendLine(string.Join($",{Environment.NewLine}    ", fields));
            }

            _typeBuilder.AppendLine("}");
            _typeBuilder.AppendLine();

            _generatedComplexTypes.Add(stringValue);

            return stringValue;
        }

        return stringValue;
    }

    private (StringBuilder, string?) Next(LoweredNode node)
    {
        return node switch
        {
            LoweredStatementStartNode statementNode => Visit(statementNode),
            LoweredLoadNode loadNode => Visit(loadNode),
            LoweredLiteralNode literalNode => Visit(literalNode),
            LoweredVariableReferenceNode variableReferenceNode => Visit(variableReferenceNode),
            LoweredFunctionReferenceNode functionReferenceNode => Visit(functionReferenceNode),
            LoweredFieldReferenceNode fieldReferenceNode => Visit(fieldReferenceNode),
            LoweredFieldReferenceByNameNode fieldReferenceByNameNode => Visit(fieldReferenceByNameNode),
            LoweredGlobalReferenceNode globalReferenceNode => Visit(globalReferenceNode),
            LoweredUnaryNode unaryNode => Visit(unaryNode),
            LoweredBinaryNode binaryNode => Visit(binaryNode),
            LoweredAssignmentNode assignmentNode => Visit(assignmentNode),
            LoweredCallNode callNode => Visit(callNode),
            LoweredReturnNode returnNode => Visit(returnNode),
            LoweredKeywordValueNode keywordValueNode => Visit(keywordValueNode),
            LoweredSizeOfNode sizeOfNode => Visit(sizeOfNode),
            LoweredIfNode ifNode => Visit(ifNode),
            LoweredCastNode castNode => Visit(castNode),
            LoweredBlockNode blockNode => Visit(blockNode),
            LoweredVariableDeclarationNode variableDeclarationNode => Visit(variableDeclarationNode),
            LoweredFunctionDeclarationNode functionDeclarationNode => Visit(functionDeclarationNode),
            LoweredConstStructNode constStructNode => Visit(constStructNode),
            LoweredGlobalDeclarationNode globalDeclarationNode => Visit(globalDeclarationNode),
            LoweredStructDeclarationNode structDeclarationNode => Visit(structDeclarationNode),
            _ => throw new NotImplementedException(node.ToString()),
        };
    }

    private (StringBuilder, string?) Visit(LoweredStatementStartNode node)
    {
        return (
            new StringBuilder($"{Environment.NewLine}# {node.Span.Start.Line} -> {node.Span.End.Line}{Environment.NewLine}"),
            null
        );
    }

    private (StringBuilder, string?) Visit(LoweredLoadNode node)
    {
        var builder = new StringBuilder();

        var (preceding, value) = Next(node.Value);
        if (preceding.Length > 0)
            Append(builder, preceding);

        builder.AppendLine($"%{_variableCount++} = load {NextDataType(node.DataType)} {value}");

        return (builder, $"%{_variableCount}");
    }

    private (StringBuilder, string?) Visit(LoweredLiteralNode node)
    {
        return (new(), node.Value);
    }

    private (StringBuilder, string?) Visit(LoweredVariableReferenceNode node)
    {
        return (new(), $"%{node.Declaration.Identifier}");
    }

    private (StringBuilder, string?) Visit(LoweredFunctionReferenceNode node)
    {
        return (new(), $"@{node.Identifier}");
    }

    private (StringBuilder, string?) Visit(LoweredFieldReferenceNode node)
    {
        var builder = new StringBuilder();
        var (preceding, value) = Next(node.Instance);
        if (preceding.Length > 0)
            builder.AppendLine(preceding.ToString());

        return (builder, $"(field_by_index {NextDataType(node.DataType)} {NextDataType(node.InstanceDataType)} {value}, {node.Index})");
    }

    private (StringBuilder, string?) Visit(LoweredFieldReferenceByNameNode node)
    {
        var builder = new StringBuilder();
        var (preceding, value) = Next(node.Instance);
        if (preceding.Length > 0)
            builder.AppendLine(preceding.ToString());

        return (new(), $"(field_by_name {NextDataType(node.DataType)} {NextDataType(node.InstanceDataType)} {value}, {node.Name})");
    }

    private (StringBuilder, string?) Visit(LoweredGlobalReferenceNode node)
    {
        return (new(), node.Identifier);
    }

    private (StringBuilder, string?) Visit(LoweredUnaryNode node)
    {
        var builder = new StringBuilder();
        var (preceding, value) = Next(node.Value);
        if (preceding.Length > 0)
            builder.AppendLine(preceding.ToString());

        return node.Operator switch
        {
            TokenKind.Minus => (builder, $"neg {NextDataType(node.DataType)} {value}"),
            TokenKind.Exclamation => (builder, $"invert {NextDataType(node.DataType)} {value}"),
            _ => throw new NotSupportedException(),
        };
    }

    private (StringBuilder, string?) Visit(LoweredBinaryNode node)
    {
        var builder = new StringBuilder();
        var (precedingLeft, valueLeft) = Next(node.Left);
        if (precedingLeft.Length > 0)
            Append(builder, precedingLeft.ToString());

        var (precedingRight, valueRight) = Next(node.Right);
        if (precedingRight.Length > 0)
            Append(builder, precedingRight.ToString());

        var dataType = NextDataType(node.DataType);
        return node.Operator switch
        {
            TokenKind.Plus => (builder, $"(add {dataType} {valueLeft}, {valueRight})"),
            TokenKind.Minus => (builder, $"(sub {dataType} {valueLeft}, {valueRight})"),
            TokenKind.Star => (builder, $"(mul {dataType} {valueLeft}, {valueRight})"),
            TokenKind.Slash => (builder, $"(div {dataType} {valueLeft}, {valueRight})"),
            TokenKind.EqualsEquals => (builder, $"(eq {dataType} {valueLeft}, {valueRight})"),
            TokenKind.NotEquals => (builder, $"(ne {dataType} {valueLeft}, {valueRight})"),
            TokenKind.EqualsEqualsEquals => (builder, $"(ref_eq {dataType} {valueLeft}, {valueRight})"),
            TokenKind.NotEqualsEquals => (builder, $"(ref_ne {dataType} {valueLeft}, {valueRight})"),
            TokenKind.Greater => (builder, $"(gt {dataType} {valueLeft}, {valueRight})"),
            TokenKind.GreaterEquals => (builder, $"(ge {dataType} {valueLeft}, {valueRight})"),
            TokenKind.Less => (builder, $"(lt {dataType} {valueLeft}, {valueRight})"),
            TokenKind.LessEquals => (builder, $"(le {dataType} {valueLeft}, {valueRight})"),
            TokenKind.AmpersandAmpersand => (builder, $"(and {dataType} {valueLeft}, {valueRight})"),
            TokenKind.PipePipe => (builder, $"(or {dataType} {valueLeft}, {valueRight})"),
            _ => throw new NotSupportedException(node.Operator.ToString()),
        };
    }

    private (StringBuilder, string?) Visit(LoweredAssignmentNode node)
    {
        var builder = new StringBuilder();
        var (precedingAssignee, valueAssignee) = Next(node.Assignee);
        if (precedingAssignee.Length > 0)
            Append(builder, precedingAssignee.ToString());

        var (precedingValue, valueValue) = Next(node.Value);
        if (precedingValue.Length > 0)
            Append(builder, precedingValue.ToString());

        builder.AppendLine($"set {NextDataType(node.Assignee.DataType)} {valueAssignee}, {valueValue}");

        return (builder, null);
    }

    private (StringBuilder, string?) Visit(LoweredCallNode node)
    {
        var builder = new StringBuilder();
        var argumentValues = new List<string>();
        foreach (var argument in node.Arguments)
        {
            var (preceding, value) = Next(argument);
            if (preceding.Length > 0)
                Append(builder, preceding.ToString());

            argumentValues.Add(NextDataType(argument.DataType) + " " + value ?? "null");
        }

        var (precedingCallee, valueCalle) = Next(node.Callee);
        if (precedingCallee.Length > 0)
            Append(builder, precedingCallee.ToString());

        builder.AppendLine($"%{_variableCount++} = call {valueCalle}({string.Join(", ", argumentValues)})");

        return (builder, $"%{_variableCount}");
    }

    private (StringBuilder, string?) Visit(LoweredReturnNode node)
    {
        var builder = new StringBuilder();
        string valueString = "";
        if (node.Value != null)
        {
            var (preceding, value) = Next(node.Value);
            if (preceding.Length > 0)
                Append(builder, preceding.ToString());

            valueString = " " + value;
        }

        builder.AppendLine($"return{valueString}");

        return (builder, null);
    }

    private (StringBuilder, string?) Visit(LoweredKeywordValueNode node)
    {
        var builder = new StringBuilder();
        var argumentValues = new List<string>();
        foreach (var argument in node.Arguments)
        {
            var (preceding, value) = Next(argument);
            if (preceding.Length > 0)
                Append(builder, preceding.ToString());

            argumentValues.Add(NextDataType(argument.DataType) + " " + value ?? "null");
        }

        return (builder, $"{node.Kind} {string.Join(", ", argumentValues)}");
    }

    private (StringBuilder, string?) Visit(LoweredSizeOfNode node)
    {
        return (new(), $"size_of {node.Argument}");
    }

    private (StringBuilder, string?) Visit(LoweredIfNode node)
    {
        var builder = new StringBuilder();
        var (precedingCondition, valueCondition) = Next(node.Condition);
        if (precedingCondition.Length > 0)
            Append(builder, precedingCondition.ToString());

        builder.AppendLine($"if {valueCondition}");
        Append(builder, Next(node.ThenBranch).Item1.ToString());

        if (node.ElseBranch != null)
        {
            builder.AppendLine("else");
            Append(builder, Next(node.ElseBranch).Item1.ToString());
        }

        return (builder, null);
    }

    private (StringBuilder, string?) Visit(LoweredCastNode node)
    {
        var builder = new StringBuilder();
        var (preceding, value) = Next(node.Value);
        if (preceding.Length > 0)
            Append(builder, preceding.ToString());

        return (builder, $"cast {NextDataType(node.DataType)} {value}");
    }

    private (StringBuilder, string?) Visit(LoweredBlockNode node)
    {
        // TODO: node.ReturnValueDeclarationNode?
        var builder = new StringBuilder();
        builder.AppendLine("{");
        foreach (var (i, value) in node.Expressions.Index())
        {
            var stringValue = Next(value)
                .Item1
                .ToString()
                .Replace(Environment.NewLine, $"{Environment.NewLine}    ");
            if (i == 0)
                stringValue = "    " + stringValue.TrimStart();

            if (i == node.Expressions.Count - 1)
                stringValue = stringValue.TrimEnd();

            builder.Append(stringValue);
        }

        builder.AppendLine();
        builder.AppendLine("}");

        return (builder, null);
    }

    private (StringBuilder, string?) Visit(LoweredVariableDeclarationNode node)
    {
        var builder = new StringBuilder();
        var valueString = string.Empty;
        if (node.Value != null)
        {
            var (preceding, value) = Next(node.Value);
            if (preceding.Length > 0)
                Append(builder, preceding.ToString());

            valueString = $" = {value}";
        }

        builder.AppendLine($"decl %{node.Identifier} {NextDataType(node.DataType)}{valueString}");

        return (builder, null);
    }

    private (StringBuilder, string?) Visit(LoweredFunctionDeclarationNode node)
    {
        var builder = new StringBuilder();
        var parameterStrings = new List<string>();
        foreach (var parameter in node.Parameters)
        {
            var parameterString = $"{NextDataType(parameter.DataType)} {parameter.Identifier}";
            if (parameter.Value != null)
                parameterString += $" = {Next(parameter.Value).Item2}";

            parameterStrings.Add(parameterString);
        }

        builder.AppendLine($"fn @{node.Identifier}({string.Join(", ", parameterStrings)})");
        if (node.Body != null)
            Append(builder, Next(node.Body).Item1.ToString());

        return (builder, null);
    }

    private (StringBuilder, string?) Visit(LoweredConstStructNode node)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.Append('{');
        builder.AppendLine();
        builder.Append("    ");

        var stringValues = node
            .Values
            .Where(x => x is not LoweredOnDemandReferencePlaceholderNode)
            .Select(x => Next(x).Item2);
        var elementListString = string.Join($",{Environment.NewLine}    ", stringValues);

        builder.AppendLine(elementListString);
        builder.Append('}');

        return (new(), builder.ToString());
    }

    private (StringBuilder, string?) Visit(LoweredGlobalDeclarationNode node)
    {
        var builder = new StringBuilder();
        builder.Append($"@{node.Identifier} {NextDataType(node.DataType)}");
        if (node.Value != null)
            builder.Append($" = {Next(node.Value).Item2}");

        builder.AppendLine();

        return (builder, null);
    }

    private (StringBuilder, string?) Visit(LoweredStructDeclarationNode node)
    {
        return (new("TODO: Struct"), null);
    }

    private static void Append(StringBuilder builder, StringBuilder value)
    {
        builder.AppendLine(value.ToString().Trim());
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.AppendLine(value.Trim());
    }
}

