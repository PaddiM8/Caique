# Variables (Statement)


## Declaration
### Syntax

See [Functions](functions.md) for parameter syntax and [Classes](classes.md) for field syntax.
```ebnf
variableDecl = "let" , identifier , [ ":" , type ] "=" expression
```

### Scope

* Root scope
* Somewhere inside function bodies
* Directly inside class and struct bodies (different syntax)
* Function parameter lists (different syntax)

## Variable expression
### Syntax
```ebnf
variableExpr = identifier ;
```