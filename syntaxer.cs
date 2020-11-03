using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Intellisense.Common;
using RoslynIntellisense;
using System.Diagnostics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("syntaxer.core.tests")]

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

namespace Syntaxer
{
    // Ports:
    // 18000 - Sublime Text 3
    // 18001 - Notepad++
    // 18002 - VSCode.CodeMap
    // 18003 - VSCode.CS-Script
    class Server
    {
        // -port:18003 -listen -timeout:60000 cscs_path:C:\Users\<user>\AppData\Roaming\Code\User\cs-script.user\syntaxer\1.2.2.0\cscs.exe

        static void test()
        {
            Int32 port = 18003;
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");

            // TcpListener server = new TcpListener(port);
            var server = new TcpListener(localAddr, port);

            // Start listening for client requests.
            server.Start();

            // Buffer for reading data
            // Byte[] bytes = new Byte[256];
            // String data = null;

            // Enter the listening loop.
            while (true)
            {
                Console.Write("Waiting for a connection... ");

                // Perform a blocking call to accept requests.
                // You could also user server.AcceptSocket() here.
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Connected!");
            }
        }

        static void TestGenerateProjectFor()
        {
            var script = @"E:\PrivateData\Galos\Projects\cs-script.core\src\cscs\bin\Debug\netcoreapp3.1\script.cs";
            csscript.cscs_path = @"E:\PrivateData\Galos\Projects\cs-script.core\src\cscs\bin\Debug\netcoreapp3.1\cscs.dll";
            var type = csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "ProjectBuilder").FirstOrDefault();
            csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "ProjectBuilder").FirstOrDefault();
            MethodInfo method = type.GetMethod("GenerateProjectFor", BindingFlags.Public | BindingFlags.Static);
            var project = method.Invoke(null, new object[] { script });
        }

        static void Main(string[] args)
        {
            // var ttt = AppDomain.CurrentDomain.GetAssemblies();
            // TestGenerateProjectFor(); return;
            // Debug.Assert(false);

            var input = new Args(args);

            // -listen -timeout:60000 -cscs_path:./cscs.exe
            // if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
            // {
            //     if (!input.dr) // not already deployed
            //         DeployRoslyn();
            // }
            // else
            // {
            //     LoadRoslyn();
            // }

            mono_root = Path.GetDirectoryName(typeof(string).Assembly.Location);
            Output.WriteLine(mono_root);
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Run(input);
        }

        static string mono_root;

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
            return Probe(mono_root, args.Name) ??
                   Probe(mono_root.PathJoin("Fasades"), args.Name) ??
                   Probe(Local_dir, args.Name) ??
                   ProbeAlreadyLoaded(args.ShortName());
        }

        static void Run(Args input)
        {
            if (input.cscs_path != null)
            {
                csscript.cscs_path = Path.GetFullPath(input.cscs_path);
            }

            if (csscript.cscs_path == null || !File.Exists(csscript.cscs_path))
            {
                Console.WriteLine("Probing cscs.exe ...");
                if (File.Exists(csscript.default_cscs_path))
                {
                    csscript.cscs_path = csscript.default_cscs_path;
                }
                else if (File.Exists(csscript.default_cscs_path2))
                {
                    csscript.cscs_path = csscript.default_cscs_path2;
                }
                else
                    Console.WriteLine("Probing cscs.exe failed...");
            }
            else
                Console.WriteLine("cscs.exe: " + csscript.cscs_path);

            if (input.test)
            {
                if (csscript.cscs_path == null)
                    csscript.cscs_path = csscript.default_cscs_path;

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