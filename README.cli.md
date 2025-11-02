# CS-Syntaxer CLI Guide

## Overview

CS-Syntaxer is a syntax provider for C# scripts (cs-script) that offers IntelliSense and code analysis services. This command-line interface provides access to syntax analysis features that can be integrated with various editors and IDEs.

At runtime interaction with between the syntaxer and the application is done via socket. Typically, the client application starts syntaxer server which starts listening to the user specified port. When client sends a request and receives the response (both are in teh plain text formats). See Commands section below.

When developing applications using syntaxer it's convenient to do testing and experiments by using a simple CLI client application that takes the request parameters as CLI arguments, sends then to teh syntaxer server and prints the response. It does for C# syntaxer the same thing that curl utility for teh web server.  

Starting syntaxer service:

```txt
D:\dev> syntaxer.exe -listen -port:18004 -timeout:600000 "-cscs_path:C:\Users\user\.dotnet\tools\.store\cs-script.cli\4.8.25\cs-script.cli\4.8.25\tools\net9.0\any\cscs.dll"
```

Sending syntaxer request:
```txt
D:\dev> syntaxer.cli.exe 18004 -op:project -script:D:\dev\spikes\WebCodeEditor\CodeMiro\test.cs
file:D:\dev\spikes\WebCodeEditor\CodeMiro\test.cs
file:D:\dev\cs-script\src\out\Windows\lib\global-usings.cs
ref:C:\Program Files\dotnet\shared\Microsoft.NETCore.App\9.0.6\System.Private.CoreLib.dll
ref:C:\Program Files\dotnet\shared\Microsoft.NETCore.App\9.0.6\System.Runtime.dll
. . .
```

## Basic Usage

## Command Options

### Help and Information

Displays version information, copyright, and available commands.

### Detection

Prints the location of the detected CS-Script installation as well as its own assembly path.

### List Running Services

Lists running services and allows you to select and terminate one.

### Terminate Services

Terminates all running syntaxer services.

### Server Operations

Starts a TCP server that listens for client requests:
- `-port`: Port to listen on (default: 18000)
- `-timeout`: Connection timeout in milliseconds (default: 5000)
- `-cscs_path`: Path to the cscs.exe executable, that discovers the script (source code) dependencies.

### Testing

Runs all tests.

## Server Commands

When the server is running, it can process the following requests:

- `references` - Find references to a symbol
- `suggest_usings` - Find appropriate using statements for unresolved symbols
- `resolve` - Resolve a symbol to its definition
- `completion` - Get completion suggestions
- `tooltip` - Get tooltip information
- `signaturehelp` - Get signature help for method calls
- `project` - Generate project information
- `codemap` - Generate code structure map
- `format` - Format code

### Socket Request Format

Requests to the syntaxer server are sent as text messages over a TCP socket connection. Each request follows this general format:

Responses are returned as plain text, with format depending on the specific command.

#### Command Details

Command request format is a '\n'-delimited lines text containing the requestror (`-client`) derails, requested operation type (`-op`) and the set of operation specific arguments:-client:{procId}\n-op:{operation}[\n-<arg>:{value}];
The command is send via Socket and the response is also a '\n'-delimited lines of text, which is to be interpreted by the requestor.

In the section below the `-client:{procId}\n` is omitted for convenience.

1. ✅ **references**
   - Request:
     ```
     -op:references 
     -script:<source_code_path>
     -pos:<caret_position>
     ```   
      *Example:*  
      ```
      -client:31368
      -op:references
      -script:C:\Users\user\AppData\Local\Temp\Roslyn.Intellisense\sources\0218af49-230f-473a-b733-13c336a6a12d.cs
      -pos:250
      ```
    - Response: JSON array of locations where the symbol is referenced
      *Example:*  
      ```
      D:\dev\spikes\WebCodeEditor\CodeMiro\test.cs(14,1): Foo();
      D:\dev\spikes\WebCodeEditor\CodeMiro\test.cs(43,10): Foo();
      ```

2. 🚩**suggest_usings**
   - Request:
     ```
     -op:suggest_usings
     -script:<source_code_path>
     -word:<unresolved_symbol>
     ```   
   - Response: List of possible using statements that would resolve the symbol

3. ✅**resolve**
   - Request:
     ```
     -op:resolve
     -script:<source_code_path>
     -pos:<cursor_position>
     [-rich]
     ```   
     *Example:*  
     ```
     -client:31368
     -op:resolve
     -script:C:\Users\user\AppData\Local\Temp\Roslyn.Intellisense\sources\28521c3f-ab1b-4635-a50f-e7ff2842e27c.cs
     -pos:227
     ```
   - Response: Location information for the symbol definition
     *Example:*  
     ```
     file:D:\dev\spikes\WebCodeEditor\CodeMiro\test.cs
     line:16
     ```
     if `-rich` parameter is present in teh request the response result is a rich serialization of the DomRegion:
     ```   
     <BeginColumn>
     <BeginLine>
     <EndLine>
     <FileName>
     <Hint>
     <IsEmpty>
     ```

4. ✅**completion**
   - Request:
     ```
     -op:completion
     -script:<source_code_path>
     -pos:<cursor_position>
     ```   
     *Example:*  
     ```
     -client:31368
     -op:completion
     -script:C:\Users\user\AppData\Local\Temp\Roslyn.Intellisense\sources\ffd13c07-698b-49ed-b60c-ed94d7cf90cd.cs
     -pos:192
     ```
   - Response: JSON array of completion items including names, types, and documentation
     *Example (truncated):*  
     ```
     BackgroundColor	property|BackgroundColor
     Beep(...)	method|Beep
     BufferHeight	property|BufferHeight
     BufferWidth	property|BufferWidth
     CancelKeyPress	event|CancelKeyPress
     CapsLock	property|CapsLock
     Clear()	method|Clear()
     CursorLeft	property|CursorLeft
     CursorSize	property|CursorSize
     . . .
     ```
5. ✅**tooltip**
   - Request:
     Internally, this request is redirected to a `memberinfo` request handler that is a more generic version of tooltip request. Though in most of the cases you should be OK with just a tooltip request.
     
     ```
     -op:tooltip[:<hint>]
     -script:<source_code_path>
     -pos:<cursor_position>
     [-short_hinted_tooltips:<1:0>]
     ```   
     *Examples:*  
     - `tooltip` request:
       `short_hinted_tooltips` is only used if hit is specified to allow more refined result of the overloads that match teh hint (e.g. for Notepad++ intellisense)
       ```
       -client:31368
       -op:tooltip
       -script:C:\Users\user\AppData\Local\Temp\Roslyn.Intellisense\sources\09fa4a1b-abf1-4272-87e7-83d8139be25d.cs
       -pos:207
       ```
          
     - `memberinfo` request:
       ```
       -client:31368
       -op:memberinfo
       -script:C:\Users\user\AppData\Local\Temp\Roslyn.Intellisense\sources\09fa4a1b-abf1-4272-87e7-83d8139be25d.cs
       -pos:207
       [-collapseOverloads]
       ```
   - Response: Formatted tooltip text with type information and documentation
     *Examples:* 
     - `tooltip` response:
       ```
       Method: void Console.WriteLine(string? value) (+ 18 overloads)
       ```
     - `memberinfo` response:
       *first line*: position of the caret
       *second line*: member info
       ```
       207
       Method: void Console.WriteLine(string? value) (+ 18 overloads)
       ```
6. 🚩**signaturehelp**
   - Request:
     ```
     -op:signaturehelp
     -script:<source_code_path>
     -pos:<cursor_position>
     ```   
   - Response: JSON object with method signature information including parameters and overloads

7. 🚩**project**
   - Request:
     ```
     -op:project
     -script:<source_code_path>
     ```   
   - Response: Project information including references and compilation options

8. 🚩**codemap**
   - Request:
     ```
     -op:codemap
     -script:<source_code_path>
     -inherited:<true|false>
     -system:<true|false>
     ```   
   - Response: Hierarchical structure of code elements (namespaces, classes, methods)

9. ✅**format**
   - Request:
     ``` 
     -op:format
     -script:<source_code_path>
     -pos:<cursor_position>
     ```   
     *Example:*  
     ```
     -client:34392
     -op:format
     -script:C:\Users\user\AppData\Local\Temp\tmpjbdjno.tmp
     -pos:224
     ```
   - Response: Formatted source code with updated cursor position
     *Example(truncated):*  
     *First line*: position of the caret after formatting
     ```
     220
     //css_ng csc
     //css_include global-usings
     ...
     ```

## Port Assignments

Default ports

- 18000 - Sublime Text 3
- 18001 - Notepad++
- 18002 - VSCode.CodeMap
- 18003 - VSCode.CS-Script
- 18004 - CS-Script's WDBG (web debugger)