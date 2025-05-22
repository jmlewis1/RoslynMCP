- Make RoslynTools a partial class, and have one tool per file.
- Create a new tool in RoslynTools in a new file but under the same class that can the documentation from roslyn for a fully qualified type name
    - This tools should also include some inheritance information, include the full inheritance as a tree in the form
    This Type: TestSln.TestProject.Person
        - SomeClass
        - SomeInterface
            -SomeBaseInterface
    etc...
- split the unit tests the same way you've split the RoslynTools, use a partial class, put the tests for each tool in a separate file
- Create unit tests that use Reflection to get the fully qualified type name of a couple of common types, and checks to see if Roslyn can find the type, and get the inheritance information, and the public interface
- Bootstrap a test that doesn't need to work that tests this against Person in TestSln.  I'm unsure of the fully qualifiied type name of Person.
