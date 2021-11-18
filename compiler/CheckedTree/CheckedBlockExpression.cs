using System;
using System.Collections.Generic;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public partial class CheckedBlockExpression : CheckedExpression
    {
        public List<CheckedStatement> Statements { get; }

        public SymbolEnvironment Environment { get; }

        public bool ReturnsLastExpression { get; }

        public CheckedBlockExpression(List<CheckedStatement> statements,
                                      SymbolEnvironment environment,
                                      IDataType dataType,
                                      bool returnsLastExpression) : base(dataType)
        {
            Statements = statements;
            Environment = environment;
            ReturnsLastExpression = returnsLastExpression;
        }

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            return new CheckedBlockExpression(
                Statements.CloneStatements(cloningInfo),
                Environment,
                DataType.Clone(cloningInfo),
                ReturnsLastExpression
            );
        }
    }
}