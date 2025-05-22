- In Program.cs use whatever standard dotnet command line args parser, and create a CLI that takes 3 parameters
    - --stdio/-s for stdio mode, and use the .WithStdioServerTransport()
    - --http for http transport
    - --help/-h for help
    - if neither stdio or http is set default to http.
- I need to be able to programatically set the port if it's not coming from the appsettings.json
- In RoslynTool I've changed the way finding the token works.  Originally we had a character position passed in
  But the model couldn't get that right, it struggles with the line number even.  I've changed it to line number and token name.
  The problem with token name is I'm just doing a string search to find its starting index.  It would be better if we went to the correct line
  Then had a way to go through all of the tokens/symbols on that line until we find one that matches the one we're looking for, case sensitive
  Then got the position from that.
- Make these changes then create a test to verify that the new token finding is working as expected using TestSln to test against.
- Unit tests are failing again, they should be fixed.