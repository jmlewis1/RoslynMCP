Add a new tool to RoslynTools that will find where a symbol is declared
- This tool will take the same params as GetDetailedSymbolInfo
- It will return the following format
filename where the symbol is declared
line number: line number where the symbol was declared
type info for the symbol by calling GetTypeDocumentation

- This needs to work on class declarations so
class MyClass
if it asks for the declaration of MyClass on that line, it should be able to figure out the type information for MyClass, and return its public interface

- Write unit tests using TestSln
 - Line 11 Person - class declaration
 - Line 17 Name - Property Declaration
 - Line 31 aField - field declaration
 - Line 59 person - a variable use declared on Line 51 of type Person.
 
 - Build and fix any errors
 - Run tests and fix any errors