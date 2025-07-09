using Caique.Lexing;

namespace Caique.Analysis;

public class ConstantFolder(Func<SemanticNode, IDataType, SemanticNode> typeCheck)
{
    private readonly Func<SemanticNode, IDataType, SemanticNode> _typeCheck = typeCheck;
    private IDataType _targetDataType = null!;

    public SemanticLiteralNode Fold(SemanticNode node, IDataType targetDataType)
    {
        _targetDataType = targetDataType;

        return Next(node);
    }

    private SemanticLiteralNode Next(SemanticNode node)
    {
        return node switch
        {
            SemanticBinaryNode binaryNode => Visit(binaryNode),
            SemanticLiteralNode literalNode => Visit(literalNode),
            _ => throw new NotSupportedException(node.ToString()),
        };
    }

    private SemanticLiteralNode Visit(SemanticBinaryNode node)
    {
        var left = Next(node.Left);
        var right = Next(node.Right);

        string stringResult;
        if (left.DataType.IsInteger())
        {
            var leftValue = ulong.Parse(left.Value.Value);
            var rightValue = ulong.Parse(right.Value.Value);
            var numericalResult = node.Operator switch
            {
                TokenKind.Plus => leftValue + rightValue,
                TokenKind.Minus => leftValue - rightValue,
                TokenKind.Star => leftValue * rightValue,
                TokenKind.Slash => leftValue / rightValue,
                _ => throw new NotSupportedException(node.Operator.ToString()),
            };
            stringResult = numericalResult.ToString();
        }
        else if (left.DataType.IsFloat())
        {
            var leftValue = double.Parse(left.Value.Value);
            var rightValue = double.Parse(right.Value.Value);
            var numericalResult = node.Operator switch
            {
                TokenKind.Plus => leftValue + rightValue,
                TokenKind.Minus => leftValue - rightValue,
                TokenKind.Star => leftValue * rightValue,
                TokenKind.Slash => leftValue / rightValue,
                _ => throw new NotSupportedException(node.Operator.ToString()),
            };
            stringResult = numericalResult.ToString();
        }
        else
        {
            throw new NotSupportedException(left.DataType.ToString());
        }

        var result = left.Value with
        {
            Value = stringResult,
        };

        return new SemanticLiteralNode(result, left.DataType);
    }

    private SemanticLiteralNode Visit(SemanticLiteralNode node)
    {
        return _typeCheck.Invoke(node, _targetDataType) as SemanticLiteralNode
            ?? throw new NotSupportedException($"Expected {_targetDataType} but got {node.DataType}.");
    }
}
