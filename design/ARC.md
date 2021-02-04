# Automatic Reference Counting

## Call/new expression
It can either be left alone without passing the pointer elsewhere, or it can for example be the value of a variable declaration. In both cases, there should be *one* retain,
and *one* release at the end of the scope. Therefore, it might make sense to simply let the call/new expressions handle this, and not eg. the variable declarations. 

## Variable declaration
Hmm, does anything need to be done here?

## Return statement
Retain.

## Block return
Retain if it belongs to a function.