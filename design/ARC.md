# Automatic Reference Counting

## Function declaration
* If a parameter is an object, retain at the start and release at the end.

## Function call
* If the return type is an object, release at the end of the scope.

## New expression
* Don't retain the newly created object, just make sure objects are initialised with a reference count of 1. Release at the end of the scope.

## Return from function
Retain.