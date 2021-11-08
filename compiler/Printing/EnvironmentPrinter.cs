using System;
using System.Linq;
using System.Text;
using Caique.CheckedTree;
using Caique.Semantics;

namespace Caique.Printing
{
    class EnvironmentPrinter
    {
        private int _indentationLevel = 1;

        public static void Print(ModuleEnvironment environment)
        {
            var printer = new EnvironmentPrinter();
            printer.PrintModule(environment);
        }

        private void PrintModule(ModuleEnvironment environment)
        {
            PrintStart("Module: " + environment.Identifier, ConsoleColor.Magenta);
            foreach (var (_, child) in environment.Modules)
            {
                PrintModule(child);
            }

            _indentationLevel--;
            PrintSymbols(environment.SymbolEnvironment);
        }

        private void PrintSymbols(SymbolEnvironment environment)
        {
            foreach (var classSymbol in environment.Classes)
            {
                PrintStructSymbol(classSymbol);
            }

            foreach (var functionSymbol in environment.Functions)
            {
                PrintFunctionSymbol(functionSymbol);
            }
        }

        private void PrintStructSymbol(StructSymbol symbol)
        {
            PrintStart("Class: " + symbol.Syntax.Identifier.Value, ConsoleColor.Green);
            if (symbol.AllChecked.Count > 1)
            {
                foreach (var checkedClass in symbol.AllChecked)
                {
                    PrintMiddle(checkedClass.FullName, ConsoleColor.Cyan);

                    foreach (var memberFunction in checkedClass.Body!.Environment.Functions)
                    {
                        var checkedFunction = checkedClass.GetFunction(memberFunction.Syntax.FullName);
                        if (checkedFunction != null)
                        {
                            _indentationLevel++;
                            PrintCheckedFunction(checkedFunction, ConsoleColor.Gray);
                            _indentationLevel--;
                        }
                    }
                }
            }

            _indentationLevel--;
        }

        private void PrintFunctionSymbol(FunctionSymbol symbol)
        {
            var parameters = new StringBuilder();
            parameters.Append('(');
            foreach (var parameter in symbol.Syntax.Parameters)
            {
                parameters.Append(parameter.SpecifiedType?.FullName);
                parameters.Append(", ");
            }

            if (parameters.Length > 1) parameters.Remove(parameters.Length - 2, 2);
            parameters.Append(')');
            PrintStart("Function: " + symbol.Syntax.FullName + parameters, ConsoleColor.Blue);

            if (symbol.AllChecked.Count > 1)
            {
                foreach (var checkedFunction in symbol.AllChecked)
                {
                    PrintCheckedFunction(checkedFunction);
                }
            }

            _indentationLevel--;
        }

        private void PrintCheckedFunction(CheckedFunctionDeclStatement checkedFunction, ConsoleColor color = ConsoleColor.Cyan)
        {
            var checkedParameters = new StringBuilder();
            checkedParameters.Append('(');
            foreach (var checkedParameter in checkedFunction.Parameters)
            {
                checkedParameters.Append(checkedParameter.DataType);
                checkedParameters.Append(", ");
            }

            if (checkedParameters.Length > 1) checkedParameters.Remove(checkedParameters.Length - 2, 2);
            checkedParameters.Append(')');
            PrintMiddle(checkedFunction.FullName + checkedParameters.ToString(), color);
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