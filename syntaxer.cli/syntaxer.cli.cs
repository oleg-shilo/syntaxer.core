//css_args -nl
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

// Shockingly there is no one transparent IPC solution for Win, Linux, Mac
// - Named-pipes are not implemented on Linux
// - Sockets seems to be a good portable approach but they claim port (not a bigi though)
//   but on Windows it will require granting special permissions. "Frightening" confirmation dialog
//   is not a good UX.
// - Unix domain socket plays the same role as named-pipes but with Socket interface: not portable
//   on Win and create file anyway. BTW the file that may be left on the system:
//   http://mono.1490590.n4.nabble.com/Unix-domain-sockets-on-mono-td1490601.html
// - Unix-pipes then closest Win named-pipes equivalent are still OS specific and create a file as well.
// ---------------------
// Bottom line: the only simple portable solution is to use a file for pushing and pulling data to and
// from the syntax server.
//  - ultimately portable
//  - fast enough (particularly with request rate we need to meet - less than 1 request per 3 sec)
//  - requires no special permissions
//  - full control of cleanup

namespace Syntaxer
{
    class Repeater
    {
        static void Main(string[] args)
        {
            try
            {
                using (var clientSocket = new TcpClient())
                {
                    var port = int.Parse(args.First());
                    clientSocket.Connect(IPAddress.Loopback, port);

                    var message = string.Join("\n", args.Skip(1).ToArray());
                    // Console.WriteLine("Sending: " + message);
                    clientSocket.WriteAllBytes(message.GetBytes());

                    string response = clientSocket.ReadAllBytes().GetString();
                    Console.WriteLine(response);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("<error>" + e.Message);
            }
        }
    }
}