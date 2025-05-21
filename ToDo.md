- Work in the git branch you are in
- Do not create or switch branches

Creating the MSBuildWorkspace is very time consuming on larger projects
I want to refactor so the MSBuildWorkspace gets created once, then reused across multiple calls

- In RoslynTool we have a good function for fetching context about a token.
- Create a new class that can handle creation of the MSBuildWorkspace
- Take all code that sets up the MSBuildWorkspace out of RoslynTool, and puts it in this new class
- Extend that MSBuildWorkspace setup to start using FileSystemWatcher
- Recurively walk the directory structure from the folder the .sln file is in, and setup a FileSystemWatcher for each subdirectory
- Have it automatically update documents in the MSBuildWorkspace when the source files change on disk
- Make sure it watches for new source files, and new sub directories with source files that will need to be monitored as well
- In RoslynTool use this new class to get the MSBuildWorkspace
- Create a service that can be used to cache the MSBuildWorkspace, so it gets created once on first use
    then can be reused on each subsequent call to the tool
- Also, I changed the tool parameters, the unit tests are all broken and need to be updated.