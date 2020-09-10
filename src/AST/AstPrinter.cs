using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;

namespace Caique.AST
{
    public class AstPrinter : IStatementVisitor<object>, IExpressionVisitor<object>
    {
        private int _indentationLevel = 1;

        public void PrintStatements(List<IStatement> statements)
        {
            foreach (var statement in statements)
            {
                statement.Accept(this);
            }
        }

        public object Visit(ExpressionStatement expressionStatement)
        {
            expressionStatement.Expression.Accept(this);

            return null!;
        }

        public object Visit(BinaryExpression binaryExpression)
        {
            PrintStart(binaryExpression.Operator.Kind.ToString(), ConsoleColor.Green);
            binaryExpression.Left.Accept(this);
            binaryExpression.Right.Accept(this);
            _indentationLevel--;

            return null!;
        }

        public object Visit(LiteralExpression literalExpression)
        {
            PrintMiddle(literalExpression.Value.Value, ConsoleColor.Magenta);

            return null!;
        }
        private void PrintStart(string value, ConsoleColor color)
        {
            PrintSection(value, color, "┣━ ");
            _indentationLevel++;
        }

        private void PrintMiddle(string value, ConsoleColor color)
        {
            PrintSection(value, color, "┣━ ");
        }

        private void PrintEnd(string value, ConsoleColor color)
        {
            PrintSection(value, color, "┗━ ");
            _indentationLevel--;
        }

        private void PrintSection(string value, ConsoleColor color, string block)
        {
            Console.Write(string.Join("", Enumerable.Repeat("┃  ", _indentationLevel - 1)));
            Console.Write(block);

            Console.ForegroundColor = color;
            Console.WriteLine(value);
            Console.ResetColor();
        }
    }
}