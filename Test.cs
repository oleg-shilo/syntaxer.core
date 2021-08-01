using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using RoslynIntellisense;

namespace Syntaxer
{
    public class Test
    {
        public static void All()
        {
            var trigegr_loadig_var = csscript.Cscs_asm;
            // Test.SuggestUsings();
            // Test.SignatureHelp();
            // Test.Resolving();
            Test.AssignmentCompletion(); ;
            Test.Renaming();
            Test.CodeMapVSCode();
            Test.Format();
            // Test.Project();
            // Test.Tooltip();

            Test.CSSCompletion();
            Test.CSSResolving();
            Test.CSSResolving2();
            Test.CSSTooltipResolving();
        }

        public static void Format()
        {
            Output.WriteLine("---");
            Output.Write("Formatting: ");

            // $safeprojectname$

            try
            {
                // var dummyWorkspace = MSBuildWorkspace.Create();
                // SyntaxTree tree = CSharpSyntaxTree.ParseText(SyntaxProvider.testCode.Trim());
                // SyntaxNode root = Microsoft.CodeAnalysis.Formatting.Formatter.Format(tree.GetRoot(), dummyWorkspace);
                var ttt = RoslynIntellisense.Formatter.FormatHybrid(SyntaxProvider.testCode, "code.cs");
                Output.WriteLine("OK");
            }
            catch (Exception e)
            {
                Output.WriteLine("failed");
                Output.WriteLine(e);
            }
        }

        public static void CodeMapVSCode()
        {
            var script = Path.GetTempFileName();

            Output.WriteLine("---");
            Output.Write("CodeMap-VSCode: ");

            try
            {
                var code = @"//css_autoclass
using System;

void main()
{
    void ttt()
    {
    }
}

//css_ac_end

static class Extensions
{
    static public void Convert(this string text)
    {
    }
}";
                File.WriteAllText(script, code);
                var map = SyntaxProvider.CodeMap(script, false, true);

                Output.WriteLine("OK");
            }
            catch (Exception e)
            {
                Output.WriteLine("failed");
                Output.WriteLine(e);
            }
            finally
            {
                try { File.Delete(script); } catch { }
            }
        }

        static void TestScript(Action<string> action, bool local = false)
        {
            var script = Path.GetTempFileName();

            if (local)
            {
                var localFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                    .PathJoin(Path.GetFileName(script));

                File.Move(script, localFile);

                script = localFile;
            }

            try
            {
                var currDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var cscs = Path.Combine(currDir, "cscs.exe");
                if (File.Exists(cscs))
                    csscript.cscs_path = cscs;
                else
                {
                    cscs = Path.Combine(Path.GetDirectoryName(currDir), "cscs.exe");
                    if (File.Exists(cscs))
                        csscript.cscs_path = cscs;
                    else
                        csscript.cscs_path = "./cscs.exe";
                }

                action(script);
            }
            catch (Exception e)
            {
                Output.WriteLine("failed");
                Output.WriteLine(e);
            }
            finally
            {
                try { File.Delete(script); } catch { }
            }
        }

        public static void AssignmentCompletion()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Console.Write("AssignmentCompletion: ");

                string code = SyntaxProvider.testCode7b;

                // System.IO.StreamReader file =

                File.WriteAllText(script, code);

                var pattern = "System.IO.StreamReader file =";
                pattern = "f.DialogResult =";
                pattern = "Form form =";
                pattern = "Form form = new";
                pattern = "Console.Wr  77";
                var caret = code.IndexOf(pattern) + pattern.Length;
                caret = caret - 4 - 3;
                string word = code.WordAt(caret);

                var completions = TestServices.GetCompletion(script, caret);

                Output.WriteLine("OK - " + completions.Count() + " completion item(s)...");
                Output.WriteLine("    '" + completions.GetLines().FirstOrDefault(x => x.StartsWith(word)) + "'");
            });
        }

        public static void CSSCompletion()
        {
            TestScript(script =>
            {
                Console.Write("CS-Script Autocompletion: ");

                File.WriteAllText(script, "  //css_inc  test.cs");

                var caret = 5;
                var completions = SyntaxProvider.GetCompletion(script, caret);

                Output.WriteLine("OK");

                caret = 12;

                completions = SyntaxProvider.GetCompletion(script, caret);

                File.WriteAllText(script, "  //css_inc  cmd.cs");
                caret = 12;

                completions = SyntaxProvider.GetCompletion(script, caret);

                caret = 15;

                completions = SyntaxProvider.GetCompletion(script, caret);

                // Console.WriteLine("    '" + completions.Split('\n').FirstOrDefault(x => x.StartsWith(word)) + "'");
            }, local: true);
        }

        public static void CSSResolving()
        {
            TestScript(script =>
            {
                Output.Write("Resolve CS-Script symbol: ");
                string code = "  //css_ref test.dll;";

                File.WriteAllText(script, code);

                var caret = 5;
                var info = TestServices.Resolve(script, caret);

                Output.WriteLine("OK");
            });
        }

        public static void CSSResolving2()
        {
            TestScript(script =>
            {
                Output.Write("Resolve CS-Script symbol: ");
                string code = "//css_inc cmd.cs;";
                // string code = "//css_ref cmd.dll;";

                File.WriteAllText(script, code);

                var caret = 13;
                var info = TestServices.Resolve(script, caret);
                Output.WriteLine(info);
                Output.WriteLine("OK");
            });
        }

        public static void CSSTooltipResolving()
        {
            TestScript(script =>
            {
                Output.Write("Resolve CS-Script symbol to tooltip: ");
                // string code = "  //css_ref test.dll;";
                string code = "  //css_inc cmd.cs;";

                File.WriteAllText(script, code);

                var caret = 13;
                // var caret = 5;

                string info = TestServices.GetTooltip(script, caret, null, true);
                Output.WriteLine(info);
                Output.WriteLine("OK");
            });
        }

        public static void Renaming()
        {
            TestScript(script =>
            {
                Output.WriteLine("---");
                Output.Write("Generate renaming info: ");
                string code = SyntaxProvider.testCodeClass;

                File.WriteAllText(script, code);

                var pattern = "class Scr";

                var caret = code.IndexOf(pattern) + pattern.Length;
                string word = code.WordAt(caret);

                var region = TestServices.FindRefreneces(script, caret, "all");

                Output.WriteLine("OK - " + 1 + " symbol info item(s)...");
                Output.WriteLine("    '" + region.GetLines().FirstOrDefault() + "'");
            });
        }
    }
}