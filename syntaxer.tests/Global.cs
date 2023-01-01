using System;
using System.IO;
using System.Reflection;
using RoslynIntellisense;
using Syntaxer;

static class Global
{
    public static void WithDisposable(Action<string> action, bool local = false)
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
                CSScriptProxy.cscs_path = cscs;
            else
            {
                cscs = @"C:\ProgramData\chocolatey\lib\cs-script\tools\cscs.dll";
                cscs = @"C:\Users\oleg.shilo\AppData\Roaming\Code\User\cs-script.user\dotnet\cscs.dll";
                // cscs = Environment.GetEnvironmentVariable("CSSCRIPT_DIR")?.PathJoin("cscs.dll");
                if (File.Exists(cscs))
                    CSScriptProxy.cscs_path = cscs;
                else
                    CSScriptProxy.cscs_path = "./cscs.exe";
            }

            action(script);
        }
        catch (Exception e)
        {
            Console.WriteLine("failed");
            Console.WriteLine(e);
        }
        finally
        {
            try { File.Delete(script); } catch { }
        }
    }

    public static T2 To<T, T2>(this T obj, Func<T, T2> mapper) => mapper(obj);

    public static string RemoveAll(this string text, string pattern) => text.Replace(pattern, "");

    public static (int caret, string code) ToTestData(this string text, string outFile, string pattern = "|")
    {
        File.WriteAllText(outFile, text.RemoveAll(pattern));
        return (text.IndexOf(pattern), text.RemoveAll(pattern));
    }
}