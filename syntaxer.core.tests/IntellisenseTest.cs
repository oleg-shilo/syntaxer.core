using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynIntellisense;
using Syntaxer;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using static Global;
using static System.Environment;

namespace syntaxer.core.tests
{
    public class IntellisenseTest
    {
        [Fact]
        public static void Completion() => WithDisposable(scriptFile =>
        {
            (var caret, _) = @"using System;
                               using System.Linq;
                               using System.Collections.Generic;

                               class Script
                               {
                                   static public void Main(string[] args)
                                   {
                                       (string message, int version) setup_say_hello()
                                       {
                                           return (""Hello from C#"", 7);
                                       }

                                       var info = setup_say_hello();

                                       print(info.message, info.ver|sion);
                                   }
                               }".ToTestData(scriptFile);

            var completions = TestServices.GetCompletion(scriptFile, caret);

            Assert.NotNull(completions.Split(NewLine).FirstOrDefault(x => x.StartsWith("version")));
        });

        [Fact]
        public static void TopLevelClassCustomComlpetion() => WithDisposable(scriptFile =>
        {
            (var caret, _) = @"using System;
                            using System.Linq;
                            using System.Collections.Generic;

                            (string message, int version) setup_say_hello()
                            {
                                return (""Hello from C#"", 7);
                            }

                            var info = setup_say_hello();

                            Console.WriteLine(info.mes|);
                            "
                            .ToTestData(scriptFile);

            var completions = TestServices.GetCompletion(scriptFile, caret);

            Assert.NotNull(completions.Split(NewLine).FirstOrDefault(x => x.StartsWith("message")));
        });

        [Fact]
        public static void TopLevelClassSystemComlpetion() => WithDisposable(script =>
        {
            (var caret, var code) = @"using System;
                                      Console.Writ|".ToTestData(script);

            var completions = TestServices.GetCompletion(script, caret).Split(NewLine);

            Assert.NotEmpty(completions);
            Assert.Contains("Write(...)	method|Write", completions);
            Assert.Contains("WriteLine(...)	method|WriteLine", completions);
        });

        [Fact]
        public static void SignatureHelp() => WithDisposable(scriptFile =>
        {
            (int caret, _) = @"using System;
                               class Program
                               {
                                   static void Main(string[] args)
                                   {
                                       Console.WriteLine(22|
                                   }
                               }"
                               .ToTestData(scriptFile);

            var info = TestServices.GetSignatureHelp(scriptFile, caret).Split(NewLine);

            /*
            12/0
            label:void Console.WriteLine()${r}${n}
            label:void Console.WriteLine(bool value)${r}${n}
            label:void Console.WriteLine(char value)${r}${n}
            ...
            */
            Assert.True(info.Skip(1).All(x => x.StartsWith("label:void Console.WriteLine(")));
        });

        [Fact]
        public static void SuggetUsings() => WithDisposable(scriptFile =>
        {
            @"using System;
              class Program
              {
                  static void Main(string[] args)
                  {
                      File
                  }
              }"
              .ToTestData(scriptFile);

            var usings = TestServices.FindUsings(scriptFile, "File").Split(NewLine);

            // System.IO
            // System.Net

            Assert.Contains("System.IO", usings);
            Assert.Contains("System.Net", usings);
        });

        [Fact]
        public static void Tooltip() => WithDisposable(scriptFile =>
        {
            (int caret, _) =

            @"using System;
                               class Program
                               {
                                   static void Main(string[] args)
                                   {
                                       Console.Write|Line(22);
                                   }
                               }"
                               .ToTestData(scriptFile);

            var tooltip = TestServices.GetTooltip(scriptFile, caret, null, true);

            // Method: void Console.WriteLine(int value) (+ 17 overloads)
            Assert.StartsWith("Method: void Console.WriteLine(int value) (+", tooltip);
        });

        [Fact]
        public static void ResolveMethod() => WithDisposable(scriptFile =>
        {
            string temp_file = null;

            try
            {
                (int caret, var code) =
                        @"using System;
                          using System.Linq;

                          class Script
                          {
                              static public void Main()
                              {
                                  Console.Write|Line(333);
                              }
                          }"
                          .ToTestData(scriptFile);

                var word = code.WordAt(caret);

                var region = TestServices.Resolve(scriptFile, caret);

                /*
                file:<file_path>
                line:<line_number>
                 */
                var info = region.Split(NewLine)
                                 .To(x => new
                                 {
                                     File = temp_file = x[0].Replace("file:", ""),
                                     Line = x[1].Replace("line:", "").ToInt()
                                 });

                var method = File.ReadAllLines(info.File)[info.Line].Trim();

                Assert.Equal("public static void WriteLine(uint value);", method);
            }
            finally
            {
                File.Delete(temp_file);
            }
        });
    }
}