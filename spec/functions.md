# Functions


## Regular function declaration
### Syntax

```ebnf
functionDecl
    = [ "pub" ]
    , [ "override" | "virtual" ]
    , "fn" , identifier , typeParameters , parameters [ , ":" , type ]
    , ( block | ";" )
    ;

parameters = "(" [ , parameter [ , { "," parameter } ] ] ")" ;
parameter = identifier [ , ":" , type ] ;
```

### External definitions
* `typeParameters` -> [Generics](generics.md)
* `type` -> [Types](types.md)

## Extension function declaration
### Syntax

```ebnf
functionDecl
    = [ "pub" ]
    , "ext" , type , identifier , typeParameters , parameters [ , ":" , type ]
    , ( block | ";" )
    ;

parameters = "(" [ , parameter [ , { "," parameter } ] ] ")" ;
parameter = identifier [ , ":" , type ] ;
```

### External definitions
* `typeParameters` -> [Generics](generics.md)
* `type` -> [Types](types.md)

## Call

### Syntax

```ebnf
callExpr = identifier [ , typeArguments ] , arguments ;
arguments = "(" [ , expression [ , { "," expression } ] ] ")" ;
```

### External definitions
* `expression` -> [Expressions](expressions.md)