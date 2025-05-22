- This test GetDetailedSymbolInfo_WithPersonType_IncludesInheritanceInformation isn't returning the public interface information.

- The GetDetailedSymbolInfo function can just call GetDetailedTypeInfo once it has the fully qualified type name, rather than reimpmeneting the functionality.
- Add a test to get person from line 52, the variable, rather than the Type
