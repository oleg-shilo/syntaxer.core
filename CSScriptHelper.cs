using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Intellisense.Common;
using RoslynIntellisense;

namespace Syntaxer
{
    public class Project
    {
        public string[] Files;
        public string[] Refs;
        public string Script;
        public string[] SearchDirs;

        public static Project GenerateProjectFor(string script)
        {
            return csscript.ProjectBuilder.GenerateProjectFor(script);
        }
    }

    //need to use reflection so cscs.exe can be remapped dynamically
    public static class csscript
    {
        internal static string default_cscs_path = Assembly.GetExecutingAssembly().Location.GetDirName().PathJoin("cscs.dll");
        internal static string default_cscs_path2 = Assembly.GetExecutingAssembly().Location.GetDirName().PathJoin("..", "cscs.dll");
        internal static string default_cscs_path3 = Environment.GetEnvironmentVariable("CSSCRIPT_ROOT").PathJoin("cscs.dll");

        internal static void Log(string message)
        {
            //string file = Path.Combine(Assembly.GetExecutingAssembly().Location + ".log");

            //if (File.Exists(file))
            //    message = File.ReadAllText(file) + "\n" + message;

            //File.WriteAllText(file, message);
            Output.WriteLine(message);
        }

        static MethodInfo GenerateProjectFor_Method;

        internal static Assembly _cscs_asm;

        internal static Assembly Cscs_asm
        {
            get
            {
                lock (typeof(csscript))
                {
                    // csscript.Log("Cscs_asm=" + (_cscs_asm == null ? "<null>" : "<asm>"));
                    // csscript.Log("cscs_path=" + cscs_path);
                    if (_cscs_asm == null)
                    {
                        try
                        {
                            if (cscs_path.IsEmpty())
                                csscript.Log($"Error: cscs_path is empty");
                            _cscs_asm = Assembly.Load(File.ReadAllBytes(cscs_path));

                            GenerateProjectFor_Method = _cscs_asm.GetLoadableTypes()
                                                                 .FirstOrDefault(t => t.Name == "ProjectBuilder")
                                                                 .GetMethod("GenerateProjectFor", BindingFlags.Public | BindingFlags.Static);

                            var runtime_class = _cscs_asm.GetLoadableTypes()
                                                         .FirstOrDefault(t => t.Name == "Runtime");

                            Environment.SetEnvironmentVariable("WINDOWS_DESKTOP_APP", (string)runtime_class.GetStaticProp("DesktopAssembliesDir"));
                            Environment.SetEnvironmentVariable("WEB_APP", (string)runtime_class.GetStaticProp("WebAssembliesDir"));
                        }
                        catch (Exception e)
                        {
                            _cscs_asm = null;
                            Log(e.ToString());

                            var error = $"Cannot load cscs.exe assembly from {cscs_path}";

                            if (e is TargetInvocationException &&
                                e.InnerException is TypeLoadException &&
                                e.InnerException.Message.Contains("System.Runtime, Version="))
                            {
                                error = error.AppendLine("Ensure that the CS-Script engine and the Syntaxer are targeting the same runtime");
                            }

                            throw new Exception(error);
                        }
                    }
                    return _cscs_asm;
                }
            }
        }

        static string _cscs_path;

        static public string cscs_path
        {
            get { return _cscs_path; }

            set
            {
                if (value != null && value != _cscs_path)
                {
                    if (value == "./cscs.exe" || !File.Exists(value))
                        _cscs_path = csscript.default_cscs_path;
                    else
                        _cscs_path = value;

                    if (_cscs_path != null && File.Exists(_cscs_path))
                        _cscs_path = Path.GetFullPath(_cscs_path);

                    Console.WriteLine("cscs_path set to: " + _cscs_path);

                    Environment.SetEnvironmentVariable("CSS_ENTRY_ASM", _cscs_path);
                }
            }
        }

        public class ProjectBuilder
        {
            static ProjectBuilder()
            {
                Environment.SetEnvironmentVariable("css_nuget", null);
            }

            static public string GetCSSConfig()
            {
                //csscript.ProjectBuilder.GetCSSConfig();
                try
                {
                    return Path.Combine(Path.GetDirectoryName(csscript._cscs_path), "css_config.xml");
                }
                catch { }
                return null;
            }

            static object cscs_GenerateProjectFor(string script)
            {
                return GenerateProjectFor_Method.Invoke(null, new object[] { script });
            }

            internal static string cscs_GetScriptTempDir()
            {
                return (string)csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "CSExecutor").First()
                                                .GetMethod("GetScriptTempDir", BindingFlags.Public | BindingFlags.Static)
                                                .Invoke(null, new object[0]);
            }

            static public Project GenerateProjectFor(string script)
            {
                //csscript.ProjectBuilder.GenerateProjectFor(script);
                try
                {
                    if (csscript.Cscs_asm == null)
                        throw new Exception($"cscs.exe assembly is not loaded ({csscript.cscs_path}).");

                    var dbg_interface_file = Path.Combine(cscs_GetScriptTempDir(), "Cache", "dbg.cs");

                    object proj = cscs_GenerateProjectFor(script);
                    Type projType = proj.GetType();

                    string[] includes = (string[])projType.GetField("Files").GetValue(proj);
                    if (File.Exists(dbg_interface_file))
                        includes = includes.Concat(new[] { dbg_interface_file }).ToArray();

                    return new Project
                    {
                        Files = includes,
                        Refs = (string[])projType.GetField("Refs").GetValue(proj),
                        SearchDirs = (string[])projType.GetField("SearchDirs").GetValue(proj),
                        Script = (string)projType.GetField("Script").GetValue(proj)
                    };
                }
                catch (TargetInvocationException e)
                {
                    throw new Exception("Cannot generate project: " + e.InnerException.Message);
                }
            }
        }

        class CSScriptProxy
        {
            static public void TriggerCompilerLoading()
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        //Preload compiler
                        {
                            var script = Path.GetTempFileName();
                            try
                            {
                                //File.WriteAllText(script, "using System;");
                                //var proc = new Process();
                                //proc.StartInfo.FileName = cscs_path;
                                //proc.StartInfo.Arguments = $"-c:0 -ac:0 \"{script}\"";
                                //proc.StartInfo.UseShellExecute = false;
                                //proc.StartInfo.CreateNoWindow = true;
                                //proc.Start();
                                //proc.WaitForExit();
                            }
                            catch { }
                            finally
                            {
                                try
                                {
                                    if (File.Exists(script))
                                        File.Delete(script);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                });
            }
        }

        public class AutoclassGenerator
        {
            // public class Result
            // {
            //     public void Init(object source)
            //     {
            //         // 'dynamic' is ideal choice but it may not work very well on Linux.
            //         // Thus reflection is more reliable for the purpose.
            //         Content = (string)source.GetField(nameof(Content));
            //         InjectionPos = (int)source.GetField(nameof(InjectionPos));
            //         InjectionLength = (int)source.GetField(nameof(InjectionLength));
            //         BodyInjectedLine = (int)source.GetField(nameof(BodyInjectedLine));
            //         BodyInjectedLineCount = (int)source.GetField(nameof(BodyInjectedLineCount));
            //         FooterInjectedLine = (int)source.GetField(nameof(FooterInjectedLine));
            //         FooterInjectedLineCount = (int)source.GetField(nameof(FooterInjectedLineCount));
            //     }

            //     public string Content;
            //     public int InjectionPos = -1;
            //     public int InjectionLength = 0;
            //     public int BodyInjectedLine = -1;
            //     public int BodyInjectedLineCount = 0;
            //     public int FooterInjectedLine = -1;
            //     public int FooterInjectedLineCount = 0;
            // }

            static public string Process(string text, ref int position)
            {
                try
                {
                    // if (csscript.Cscs_asm.GetName().Version >= new Version("3.28.7.0"))
                    // {
                    //     return ProcessNew(text, ref position, ref result);
                    // }
                    // else
                    // {
                    return ProcessOld(text, ref position);
                    // }
                }
                catch { }
                return null;
            }

            // static public string ProcessNew(string text, ref int position, ref Result result)
            // {
            //     // AutoclassPrecompiler.Result result = AutoclassPrecompiler.Process(code, ConsoleEncoding);
            //     // injectionPos = result.InjectionPos;
            //     // injectionLength = result.InjectionLength;
            //     // return result.Content;
            //     try
            //     {
            //         var type = csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "AutoclassPrecompiler").FirstOrDefault();
            //         MethodInfo method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == "Process" && m.GetParameters().Length == 2);
            //         object[] args = new object[] { text, "utf-8" };
            //         var retval = method.Invoke(null, args);

            //         result.Init(retval);

            //         position = result.InjectionPos;

            //         return result.Content;
            //     }
            //     catch { }
            //     return null;
            // }

            static public string ProcessOld(string text, ref int position)
            {
                //csscript.AutoclassGenerator.Process(text, ref position);
                try
                {
                    var type = csscript.Cscs_asm.GetLoadableTypes().Where(t => t.Name == "AutoclassGenerator").FirstOrDefault();
                    MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == "Process" && m.GetParameters().Length == 2);
                    object[] args = new object[] { text, position };
                    var result = (string)method.Invoke(null, args);
                    position = (int)args[1];
                    return result;
                }
                catch { }
                return null;
            }
        }
    }

    static class CSScriptHelper
    {
        static public Project GenerateProjectFor(SourceInfo script)
        {
            if (script.File == script.RawFile)
            {
                return Project.GenerateProjectFor(script.File);
            }
            else
            {
                // The input file is not the actual script file.
                // For example the script file in the editor is modified but unsaved and the editor
                // uses temp file for intellisense.
                // However this screws relative paths, so need to save it in the same dir if possible.

                string tempFile = "";

                try
                {
                    try
                    {
                        // Need to ensure the original dir is used for probing if the temp script is stored in
                        // Path.GetTempPath().
                        // var dir_instruction = "//css_dir " + script.File.GetDirName();
                        // File.WriteAllText(tempFile, dir_instruction + Environment.NewLine + script.Content);
                        tempFile = Path.Combine(script.File.GetDirName(), ".temp." + script.File.GetFileExtension());
                        File.WriteAllText(tempFile, script.Content);
                    }
                    catch
                    {
                        // may fail if the dir is not enough write privileges
                        tempFile = script.File;
                    }

                    var project = Project.GenerateProjectFor(tempFile);

                    project.Script = script.File;

                    var files = project.Files.ToList();
                    files.Remove(tempFile);
                    files.Insert(0, script.File);
                    project.Files = files.ToArray();

                    var dirs = project.SearchDirs.ToList();
                    dirs.Remove(tempFile.GetDirName());
                    project.SearchDirs = dirs.ToArray();

                    return project;
                }
                finally
                {
                    if (tempFile != "" && File.Exists(tempFile) && tempFile != script.File)
                        try { File.Delete(tempFile); } catch { }
                }
            }
        }

        static Project GenerateProjectFor(string script)
        {
            return Project.GenerateProjectFor(script);
        }

        static public string GetCSSConfig()
        {
            return csscript.ProjectBuilder.GetCSSConfig();
        }

        static string help_file;

        static public string GetCSSHelp()
        {
            var file = csscript.ProjectBuilder.cscs_GetScriptTempDir().PathJoin("help.txt");
            if (help_file == null)
            {
                try
                {
                    var output = Utils.Run(csscript.cscs_path, "-help");
                    File.WriteAllText(file, output);
                }
                catch
                {
                    help_file = file;
                }
            }
            return help_file ?? file;
        }

        public static CodeMapItem[] GetMapOf(string code, bool stripInjectedClass = false)
        {
            bool injected = DecorateIfRequired(ref code);
            CodeMapItem[] map = Autocompleter.GetMapOf(code, injected);

            if (injected && map.Any())
            {
                var lines = code.Split("\n".ToCharArray(), map.Last().Line + 5) // +5 to ensure the current line is in
                                .Select(x => x.Trim())
                                .ToArray();

                var injectionMarker = "///CS-Script auto-class generation";
                var debugSymbol = "#line ";

                map = map.Where(i => !lines[i.Line - 1].EndsWith(injectionMarker)).ToArray();

                foreach (CodeMapItem item in map)
                {
                    var injectedLinesAbove = lines.Take(item.Line - 1)
                                                  .Count(x => x.EndsWith(injectionMarker) ||
                                                              x.StartsWith(debugSymbol));
                    item.Line -= injectedLinesAbove;
                }
            }

            return map;
        }

        static public bool DecorateIfRequired(ref string text)
        {
            int dummy = 0;
            return DecorateIfRequired(ref text, ref dummy);
        }

        static public bool DecorateIfRequired(ref string text, ref int currentPos)
        {
            if (NeedsAutoclassWrapper(text))
            {
                int originalPos = currentPos;
                string originaltext = text;

                text = GenerateAutoclassWrapper(text, ref currentPos);

                if (!text.Contains("///CS-Script auto-class generation"))
                {
                    currentPos = originalPos;
                    return false;
                }
                else
                    return true;
            }
            else
                return false;
        }

        static public string GenerateAutoclassWrapper(string text, ref int position)
        {
            return csscript.AutoclassGenerator.Process(text, ref position);
        }

        static public bool NeedsAutoclassWrapper(string text)
        {
            // csscript.Log("NeedsAutoclassWrapper");

            bool isAutoClassSupported = false;
            try
            {
                var file = GetCSSConfig();
                if (File.Exists(file))
                {
                    var xml = XDocument.Load(file);

                    string[] defaultArgs = xml.Root.Descendants("defaultArguments")
                                                   .First()
                                                   .Value
                                                   .Split(' ');

                    if (defaultArgs.Contains("-ac") || defaultArgs.Contains("-autoclass"))
                        isAutoClassSupported = true;
                }
            }
            catch { }

            foreach (Match item in Regex.Matches(text, @"\s?//css_args\s+(/|-)(ac|ac:0|ac:1)(,|;\s+)"))
                isAutoClassSupported = !item.Value.Contains("ac:0");

            foreach (Match item in Regex.Matches(text, @"\s?//css_args\s+(/|-)(autoclass|autoclass:0|autoclass:1)(,|;|\s+)"))
                isAutoClassSupported = !item.Value.Contains("ac:0");

            foreach (Match item in Regex.Matches(text, @"\s?//css_autoclass\s+"))
                isAutoClassSupported = true;

            foreach (Match item in Regex.Matches(text, @"\s?//css_ac\s+"))
                isAutoClassSupported = true;

            return isAutoClassSupported;
        }

        static public void Undecorate(string text, ref DomRegion region)
        {
            int line = region.BeginLine;
            Undecorate(text, ref line);
            region.BeginLine = line;
            region.EndLine = line;
        }

        static public void Undecorate(string text, ref int beginLine)
        {
            int pos = text.IndexOf("///CS-Script auto-class generation");
            if (pos != -1)
            {
                // BeginLine is 1-based
                // injectedLine is 0-based
                var injectedLine = text.Substring(0, pos).Split('\n').Count() - 1;
                if (injectedLine < beginLine)
                {
                    // hardcode it for now but in the future rely on the
                    // result structure produced by DecorateIfRequired

                    int injectedLines = 0;
                    var lines = text.Substring(pos).GetLines();
                    int index = injectedLine;
                    string prevLine = null;
                    foreach (var line in lines)
                    {
                        index++;

                        if (line.StartsWith("#line ")
                            || line.Contains("///CS-Script auto-class generation"))
                            injectedLines++;

                        if (index == beginLine)
                            break;

                        prevLine = line;
                    }

                    beginLine -= injectedLines;
                }
            }
        }

        static public void NormaliseFileReference(ref string file, ref int line)
        {
            try
            {
                if (file.EndsWith(".g.csx") || file.EndsWith(".g.cs") && file.Contains(Path.Combine("CSSCRIPT", "Cache")))
                {
                    //it is an auto-generated file so try to find the original source file (logical file)
                    string dir = Path.GetDirectoryName(file);
                    string infoFile = Path.Combine(dir, "css_info.txt");
                    if (File.Exists(infoFile))
                    {
                        string[] lines = File.ReadAllLines(infoFile);
                        if (lines.Length > 1 && Directory.Exists(lines[1]))
                        {
                            string logicalFile = Path.Combine(lines[1], Path.GetFileName(file).Replace(".g.csx", ".csx").Replace(".g.cs", ".cs"));
                            if (File.Exists(logicalFile))
                            {
                                string code = File.ReadAllText(file);
                                int pos = code.IndexOf("///CS-Script auto-class generation");
                                if (pos != -1)
                                {
                                    int injectedLineNumber = code.Substring(0, pos).Split('\n').Count() - 1;
                                    if (injectedLineNumber <= line)
                                        line -= 1; //a single line is always injected
                                }
                                file = logicalFile;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}