# Expressions

## Precedence
### Syntax
```ebnf
expr = binaryExpr ;
binaryExpr = ? dotExpr <operator> dotExpr ? ;
castExpr = primaryExpr [ , "as" , type ] ;
primaryExpr
    = literalExpr
    | groupExpr
    | memberAccessExpr
    | invocationExpr
    | blockExpr
    | keywordValueExpr
    | simpleNameExpr
    | callExpr
    | newExpr
    | ifExpr
    ;
```

### External definitions
* `variableExpr` -> [Variables](variables.md)
* `callExpr` -> [Functions](functions.md)
* `newExpression` -> [Classes](classes.md)

## Literal
```ebnf
literalExpr
    = string
    | number
    | "true"
    | "false"
    ;
```

## Group
### Syntax
```ebnf
groupExpr = "(" , expression , ")" ;
```

## Member access
### Syntax
```ebnf
memberAccessExpr = primaryExpr , "." , identifier [ , typeArguments ] ;
```

### External definitions
* `typeArguments` -> [Generics](generics.md)

## Invocation
### Syntax
```ebnf
invocationExpr = primaryExpr , arguments ;
```

## Block
### Syntax
```ebnf
blockExpr = "{" , { statement } , "}" ;
```

## Keyword value
### Syntax
```ebnf
keywordValueExpr
    = self
    | super
    | sizeof
    | array_get
    | array_set
    ;
```

## Simple name
### Syntax
```ebnf
simpleNameExpr = identifier [ , typeArguments ] ;
```

### External definitions
* `typeArguments` -> [Generics](generics.md)

## If
### Syntax
```ebnf
ifExpr = "if" , expression [ , ":" ] , expression ;
```