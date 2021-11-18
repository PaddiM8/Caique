using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Parsing;
using Caique.Semantics;

namespace Caique.CheckedTree
{
    public class CheckedTypeExpression : CheckedExpression
    {
        public CheckedTypeExpression(IDataType dataType)
            : base(dataType)
        {
        }

        public override CheckedExpression Clone(CheckedCloningInfo cloningInfo)
        {
            /*if (cloningInfo.TypeParameters != null && DataType is GenericType genericType)
            {
                foreach (var (typeParameter, typeArgument) in cloningInfo.TypeParameters.Zip(cloningInfo.TypeArguments!))
                {
                    if (genericType.Identifier.Value == typeParameter.Value)
                        return new CheckedTypeExpression(typeArgument);
                }
            }*/

            return new CheckedTypeExpression(DataType.Clone(cloningInfo));
        }
    }
}