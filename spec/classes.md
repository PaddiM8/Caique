# Classes

## Declaration
### Syntax
```ebnf
classDecl
    = "class" , identifier [ , typeParameters ] [ , "<" type [ , { "," type } ] ]
    , blockExpr
    ;
```

### External definitions
* `type` -> [Types](types.md)
* `typeParameters` -> [Generics](generics.md)