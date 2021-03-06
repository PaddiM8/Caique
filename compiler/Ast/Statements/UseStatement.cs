﻿using System;
using System.Collections.Generic;
using Caique.Parsing;

namespace Caique.Ast
{
    public class UseStatement : Statement
    {
        public List<Token> ModulePath { get; }

        public UseStatement(List<Token> modulePath, TextSpan span)
            : base(span)
        {
            ModulePath = modulePath;
        }
    }
}