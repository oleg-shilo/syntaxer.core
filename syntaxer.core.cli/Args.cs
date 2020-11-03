using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Syntaxer
{
    public class Args
    {
        public Args(string[] args)
        {
            this.InitFromArgs(args);
        }

        public bool listen;
        public bool exit;
        public bool test;
        public bool rich;
        public bool doc;
        public bool dr; // deploy roslyn
        public string[] @ref;
        public string cmd;
        public string script;
        public int client;
        public int port = 18000;
        public int pos;
        public bool pkill;
        public bool collapseOverloads;
        public int short_hinted_tooltips = 1;
        public string popen;
        public int pid;
        public string pname;
        public string op;
        public string context;
        public string cscs_path;
        public string[] inc;
        public int timeout = 5000;
    }
}