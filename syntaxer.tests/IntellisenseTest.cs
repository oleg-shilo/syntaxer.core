using System;
using System.Diagnostics;
using static System.Environment;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Global;
using RoslynIntellisense;
using Syntaxer;
using Xunit;

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
        public static void Comlpetion_in_Nuget_packages() => WithDisposable(scriptFile =>
        {
            (var caret, _) = @"
//css_nuget NLog;
using System;

var t = typeof(NLog.Config.Ad|);"
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
        public static void Format() => WithDisposable(scriptFile =>
        {
            (var caret, var code) = ("\n" +
            "using System;\n" +
            "          class Program\n" +
            "              {\n" +
            "\n" +
            "\n" +
            "static void Main(|string[] args)\n" +
            "   {\n" +
            " }\n" +
            "}")
                .ToTestData(scriptFile);
            var newPos = caret;
            var newCode = TestServices.FormatCode(scriptFile, ref newPos);
            var codeAfterCaret = code.Substring(caret);
            var newCodeAfterCaret = newCode.Substring(newPos);
            Assert.Equal(codeAfterCaret.Substring(0, 10), newCodeAfterCaret.Substring(0, 10));
        });

        [Fact]
        public static void FindReferences() => WithDisposable(scriptFile =>
        {
            (var caret, var code) = @"
                using System;
                class Program
                {
                    static void Main(st|ring[] args)
                    {
                        string test = """";
                        Console.WriteLine(""Hello, World!"");
                     }
                    }
                )"
                .ToTestData(scriptFile);
            var newPos = caret;
            var newCode = TestServices.FindRefreneces(scriptFile, newPos);

            // Assert.Equal(codeAfterCaret.Substring(0, 10), newCodeAfterCaret.Substring(0, 10));
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
            //var tooltip = TestServices.GetTooltip(scriptFile, caret, "(8,5,7)", false);

            Console.WriteLine(22);

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