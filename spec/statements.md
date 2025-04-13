# Statements

## Syntax

```ebnf
statement
    = returnStmt
    | assignmentStmt
    | variableDecl
    | functionDecl
    | classDecl
    | structDecl
    | interfaceDecl
    | expressionStmt [ , ";" ]
    ;

expressionStmt = expression ;
```

## External definitions
* `variableDecl` -> [Variables](variables.md)
* `functionDecl` -> [Functions](functions.md)
* `classDecl` -> [Classes](classes.md)
* `structDecl` -> [Structs](structs.md)
* `interfaceDecl` -> [Interfaces](interfaces.md)
* `expression` -> [Expressions](expressions.md)