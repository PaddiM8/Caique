using System;
using System.Collections.Generic;
using System.Linq;
using Caique.Semantics;

static class SemanticUtils
{
        public static string GetTypeArgumentString(List<IDataType>? typeArguments)
        {
            return typeArguments != null && typeArguments.Any()
                ? "[" + string.Join(",", typeArguments.Select(x => x.ToString())) + "]"
                : "";
        }
}