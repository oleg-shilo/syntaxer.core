using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Intellisense.Common;
using RoslynIntellisense;

[assembly: InternalsVisibleTo("syntaxer.tests")]

//using csscript;

// Shockingly there is no one truly transparent IPC solution for Win, Linux, Mac
// - Named-pipes are not implemented on Linux
// - Sockets seems to be a good portable approach but they claim port (no biggie though)
// but on Windows opening socket server will require granting special permissions. Meaning a
// "frightening" confirmation dialog that is not a good for UX.
//  - Unix domain socket plays the same role as named-pipes but with Socket interface: not portable
// on Win and create file anyway. BTW the file that may be left on the system:
//  http://mono.1490590.n4.nabble.com/Unix-domain-sockets-on-mono-td1490601.html
// - Unix-pipes then closest Win named-pipes equivalent are still OS specific and create a file as well.
// ---------------------
// Bottom line: the only a compromise solution, which is simple and portable is to use a plain socket.
//  - ultimately portable
//  - fast enough (particularly with request rate we need to meet - less than 1 request per 3 sec)
//  - requires no special permissions on Linux (even if it does on Win)
//  - full control of cleanup (as there is none)

/// Ports:
/// 18000 - Sublime Text 3
/// 18001 - Notepad++
/// 18002 - VSCode.CodeMap
/// 18003 - VSCode.CS-Script
namespace Syntaxer
{
    class Server
    {
        // -port:18003 -listen -timeout:60000 cscs_path:C:\Users\<user>\AppData\Roaming\Code\User\cs-script.user\syntaxer\1.2.2.0\cscs.exe
        static void Main(string[] args)
        {
#if DEBUG
            foreach (var process in Process.GetProcessesByName("dotnet").Where(x => x.Id != Process.GetCurrentProcess().Id))
                try
                {
                    process.Kill();
                }
                catch { }
#endif
            if (!args.Any() || args.Contains("-?") || args.Contains("--help") || args.Contains("-help"))
            {
                Console.WriteLine("CS-Syntaxer v" + Assembly.GetExecutingAssembly().GetName().Version);
                Console.WriteLine("Syntax provider for C# scripts (cs-script).");
                Console.WriteLine("Copyright (C) 2022 Oleg Shilo (github.com/oleg-shilo/syntaxer.core)");
                Console.WriteLine("Path: " + Assembly.GetExecutingAssembly().Location);
                Console.WriteLine("------------");
                Console.WriteLine("-detect");
                Console.WriteLine("    Prints location of the detected CS-Script installation as well as its own assembly path.");
                Console.WriteLine("-kill");
                Console.WriteLine("    Terminates all currently running syntaxer services.");
                Console.WriteLine("    It's an equivalent of `syntaxer -list kill *` command.");
                Console.WriteLine("-list|-ls [<kill|k> [*]]");
                Console.WriteLine("    Prints list of all currently running syntaxer services.");
                Console.WriteLine("    kill|k - Allow user to select and terminate any running syntaxer service.");
                Console.WriteLine("    * - Allow user to terminate all running syntaxer service when 'kill' option is used.");
                Console.WriteLine("    *   (e.g. syntaxer -list kill * ).");

                return;
            }
            else if (args.Contains("-detect"))
            {
                Console.WriteLine(CSScriptHelper.Detect());
                return;
            }
            else if (args.FirstOrDefault() == "-kill")
            {
                var result = Runtime.GetScriptProcessLog();
                foreach (var pid in result.processes.Select(x => x.pid).Where(x => x != 0))
                    try { Process.GetProcessById(pid).Kill(); }
                    catch { }
                return;
            }
            else if (args.Contains("-list") || args.Contains("-ls"))
            {
                var result = Runtime.GetScriptProcessLog();

                if (!result.processes.Any())
                {
                    Console.WriteLine("No syntaxer process found.");
                }
                else
                {
                    if (args.Contains("kill") || args.Contains("-kill") || args.Contains("k"))
                    {
                        if (args.Contains("*"))
                        {
                            foreach (var pid in result.processes.Select(x => x.pid).Where(x => x != 0))
                                try { Process.GetProcessById(pid).Kill(); }
                                catch { }
                        }
                        else
                        {
                            Console.WriteLine(result.view);
                            Console.WriteLine("Enter index of the syntaxer process you want to terminate or '*' to terminate them all:");
                            var userInput = Console.ReadLine().ToLower();
                            if (userInput == "*")
                            {
                                foreach (var pid in result.processes.Select(x => x.pid).Where(x => x != 0))
                                    try { Process.GetProcessById(pid).Kill(); }
                                    catch { }
                            }
                            else
                            {
                                var pid = result.processes.FirstOrDefault(x => x.index == userInput).pid;
                                if (pid != 0)
                                    try
                                    {
                                        Process.GetProcessById(pid).Kill();
                                    }
                                    catch { }
                                else
                                    Console.WriteLine("Invalid user input.");
                            }
                        }
                    }
                    else
                        Console.WriteLine(result.view);
                }

                return;
            }

            var input = new Args(args);

            // -listen -timeout:60000 -cscs_path:./cscs.exe

            asm_root_dir = Path.GetDirectoryName(typeof(string).Assembly.Location); // SDK deployment
            Output.WriteLine(asm_root_dir);
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Runtime.LogScriptProcess();
            Run(input);
        }

        static string asm_root_dir;

        static string local_dir;

        static string Local_dir
        {
            get
            {
                // must be assigned here as if it is assigned in the field declaration it triggers premature assembly loading.
                return local_dir = local_dir ?? Assembly.GetExecutingAssembly().Location.GetDirName();
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Probe(asm_root_dir, args.Name) ??
                   Probe(asm_root_dir.PathJoin("Facades"), args.Name) ??
                   Probe(Local_dir, args.Name) ??
                   ProbeAlreadyLoaded(args.ShortName());
        }

        static void Run(Args input)
        {
            if (input.cscs_path != null)
            {
                // Console.WriteLine($"Input {input.cscs_path} ...");
                // Console.WriteLine($"Probing 0 {csscript.cscs_path} ...");
                CSScriptProxy.cscs_path = Path.GetFullPath(input.cscs_path);
            }
            Console.WriteLine($"Probing {CSScriptProxy.cscs_path} ...");

            if (CSScriptProxy.cscs_path == null || !File.Exists(CSScriptProxy.cscs_path))
            {
                Console.WriteLine("Probing cscs.exe ...");
                if (File.Exists(CSScriptProxy.default_cscs_path))
                {
                    CSScriptProxy.cscs_path = CSScriptProxy.default_cscs_path;
                }
                else if (File.Exists(CSScriptProxy.default_cscs_path2))
                {
                    CSScriptProxy.cscs_path = CSScriptProxy.default_cscs_path2;
                }
                else if (File.Exists(CSScriptProxy.default_cscs_path3))
                {
                    CSScriptProxy.cscs_path = CSScriptProxy.default_cscs_path3;
                }
                else
                    Console.WriteLine("Probing cscs.exe failed...");
            }
            else
                Console.WriteLine("cscs.exe: " + CSScriptProxy.cscs_path);

            if (input.test)
            {
                if (CSScriptProxy.cscs_path == null)
                    CSScriptProxy.cscs_path = CSScriptProxy.default_cscs_path;

                Test.All();
            }
            else
            {
                if (input.listen)
                    SocketServer.Listen(input);
                else
                    Output.WriteLine(SyntaxProvider.ProcessRequest(input));
            }
        }

        static Assembly Probe(string dir, string asmName)
        {
            var file = Path.Combine(dir, asmName.Split(',')[0] + ".dll");
            if (File.Exists(file))
                return Assembly.LoadFrom(file);
            else
                return null;
        }

        static Assembly ProbeAlreadyLoaded(string asmName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == asmName).FirstOrDefault();
        }
    }

    class SocketServer
    {
        static Dictionary<int, object> connections = new Dictionary<int, object>();

        static void MonitorConnections(int connectionTimeout, Action requestShutdown)
        {
            do
            {
                Thread.Sleep(connectionTimeout);
                lock (connections)
                {
                    foreach (int id in connections.Keys.ToArray())
                        if (!Utils.IsProcessRunning(id))
                            connections.Remove(id);
                }
            }
            while (connections.Any());
            requestShutdown();
        }

        public static void Listen(Args processArgs)
        {
            try
            {
                var serverSocket = new TcpListener(IPAddress.Loopback, processArgs.port);
                serverSocket.Start();

                if (processArgs.client != 0)
                {
                    connections[processArgs.client] = true;
                    Output.WriteLine("Monitor client: " + processArgs.client);
                }

                Task.Run(() => MonitorConnections(processArgs.timeout, requestShutdown: serverSocket.Stop));

                Output.WriteLine($" >> Server (v{Assembly.GetExecutingAssembly().GetName().Version}) Started (port={processArgs.port})");
                new Engine().Preload();
                Output.WriteLine($" >> Syntax engine loaded");

                while (true)
                {
                    Output.WriteLine(" >> Waiting for client request...");
                    TcpClient clientSocket = serverSocket.AcceptTcpClient();
                    Output.WriteLine(" >> Accepted client...");

                    lock (connections)
                    {
                        try
                        {
                            Output.WriteLine(" >> Reading request...");
                            string request = clientSocket.ReadAllText();

                            var args = new Args(request.GetLines());

                            if (args.exit)
                            {
                                clientSocket.WriteAllText("Bye");
                                break;
                            }
                            else
                            {
                                if (args.client != 0)
                                {
                                    connections[args.client] = true;
                                    // Output.WriteLine("Monitor client: " + args.client);
                                }
                            }

                            Output.WriteLine(" >> Processing client request");

                            string response = SyntaxProvider.ProcessRequest(args);
                            if (response != null)
                                clientSocket.WriteAllText(response);
                        }
                        catch (Exception e)
                        {
                            Output.WriteLine(e.Message);
                        }
                    }
                }

                serverSocket.Stop();
                Output.WriteLine(" >> exit");
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10048)
                    Output.WriteLine(">" + e.Message);
                else
                    Output.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Output.WriteLine(e);
            }
        }
    }

    public static class TestServices
    {
        // "references" - request
        public static string FindRefreneces(string script, int offset, string context = null) => SyntaxProvider.FindRefreneces(script, offset, context);

        // "suggest_usings" - request
        public static string FindUsings(string script, string word) => SyntaxProvider.FindUsings(script, word, false);

        // "resolve" - request
        public static string Resolve(string script, int offset)
            => SyntaxProvider.Resolve(script, offset, false);

        // public static DomRegion Resolve(string script, int offset) => SyntaxProvider.ResolveRaw(script, offset);

        // "completion" - request
        public static string GetCompletion(string script, int offset)
            => SyntaxProvider.GetCompletion(script, offset);

        // public static IEnumerable<ICompletionData> GetCompletion(string script, int offset) => SyntaxProvider.GetCompletionRaw(script, offset);

        // "tooltip" - request
        public static string GetTooltip(string script, int offset, string hint, bool shortHintedTooltips)
            => SyntaxProvider.GetTooltip(script, offset, hint, shortHintedTooltips);

        // "signaturehelp" - request
        public static string GetSignatureHelp(string script, int offset)
            => SyntaxProvider.GetSignatureHelp(script, offset);

        // "project" - request
        public static Project GenerateProjectFor(string script)
            => CSScriptHelper.GenerateProjectFor(new SourceInfo(script));

        // "codemap" - request
        public static string GetCodeMap(string script)
            => SyntaxProvider.CodeMap(script, false, false);

        // "format" - request
        public static string FormatCode(string script, ref int caretPos)
            => SyntaxProvider.FormatCode(script, ref caretPos);
    }
}

class Runtime
{
    internal static ((string index, int pid)[] processes, string view) GetScriptProcessLog()
    {
        var result = new List<(string index, int pid)>();

        var currentProcId = Process.GetCurrentProcess().Id;
        var i = 0;

        var builder = new StringBuilder();
        builder.AppendLine($"#   | PID        | Arguments");
        builder.AppendLine($"----------------------------");
        foreach ((int pid, string args) in Runtime.GetScriptProcesses().OrderByDescending(x => x.pid == currentProcId))
        {
            var current = pid == currentProcId ? "*" : " ";
            builder.AppendLine($"{current}{++i:D2} | {pid:D10} | {args}");
            result.Add((i.ToString(), pid));
        }

        return (result.ToArray(), builder.ToString());
    }

    static (int pid, string args)[] GetScriptProcesses()
    {
        (int pid, string args)[] result = null;

        {
            List<string> lines = null;
            using (var mutex = new Mutex(false, mutexName))
            {
                try
                {
                    mutex.WaitOne();

                    try
                    {
                        if (File.Exists(filePath))
                            lines = File.ReadAllLines(filePath).ToList();
                    }
                    catch { }
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            if (lines == null)
                result = [(0, "<syntaxer tracking information is not available>")];
            else
                result = lines.Select(x =>
                {
                    var parts = x.Split(':', 2);
                    try
                    {
                        if (int.TryParse(parts.First(), out int pid))
                            return (pid, parts.Last()?.Trim());
                    }
                    catch
                    {
                    }
                    return (0, "<invalid syntaxer tracking information>");
                })
                    .Where(x =>
                           {
                               try
                               {
                                   return Process.GetProcessById(x.Item1) != null;
                               }
                               catch { return false; }
                           })
                               .ToArray();
        }

        return result;
    }

    static string mutexName = "Global\\CSSyntaxerProcessListMutex"; // Global name for cross-process synchronization
    static string filePath = Environment.SpecialFolder.LocalApplicationData.GetPath().PathJoin("cs-syntaxer", "p-list");

    static bool IsRunningProcess(string processLog)
    {
        try
        {
            // <pid>: <args>
            return Process.GetProcessById(int.Parse(processLog.Split(':', 2)[0])) != null;
        }
        catch { return false; }
    }

    internal static void ClearScriptProcessLog()
    {
        using (var mutex = new Mutex(false, mutexName))
        {
            try
            {
                mutex.WaitOne();

                List<string> lines = null;

                if (File.Exists(filePath))
                    lines = File.ReadAllLines(filePath).Where(IsRunningProcess).ToList();

                if (lines != null)
                    File.WriteAllLines(filePath, lines);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    internal static void LogScriptProcess()
    {
        using (var mutex = new Mutex(false, mutexName))
        {
            try
            {
                mutex.WaitOne();
                filePath.EnsureFileDir();
                var currentProcess = $"{Process.GetCurrentProcess().Id}: {Environment.CommandLine}";

                List<string> lines = File.Exists(filePath)
                                         ? File.ReadAllLines(filePath).Where(IsRunningProcess).ToList()
                                         : [];

                lines.Add(currentProcess);

                File.WriteAllLines(filePath, lines.ToArray());
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}