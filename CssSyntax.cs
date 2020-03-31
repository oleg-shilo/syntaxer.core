using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Intellisense.Common;
using System.Text.RegularExpressions;

namespace Syntaxer
{
    class CssSyntax
    {
        static string GetHelpFile()
        {
            string file = Path.Combine(Path.GetTempPath(), "Sublime.CS-Script", "ReflctedTypes", "cs-script." + csscript.Cscs_asm.GetName().Version + ".help.txt");

            if (!File.Exists(file))
            {
                Path.GetDirectoryName(file)
                    .EnsureDir()
                    .DeleteDirFiles("cs-script.*.help.txt");

                try { File.WriteAllText(file, Utils.Run(csscript.cscs_path, "-?")); }
                catch { }
            }
            return file;
        }

        public static DomRegion Resolve(string directive)
        {
            string helpFile = GetHelpFile();

            if (File.Exists(helpFile))
            {
                string[] lines = File.ReadAllLines(helpFile);

                var matchingLine = FindSection(directive, lines);

                if (matchingLine == -1 && (directive == "//css_inc" || directive == "//css_include"))
                {
                    //'include' aliases are described under main 'import' section in older documentation
                    directive = "//css_import";
                    matchingLine = FindSection(directive, lines);
                }

                //DomRegion is one based
                if (matchingLine != -1)
                    return new DomRegion { FileName = helpFile, BeginLine = matchingLine + 1, BeginColumn = 0 };
            }

            return DomRegion.Empty;
        }

        static int FindSection(string directive, string[] lines)
        {
            string pattern1 = directive;
            string pattern2 = "Alias - " + directive;

            return lines.FindIndex(x => x.StartsWith(pattern1) || x.StartsWith(pattern2));
        }
    }

    public class CssCompletionData : ICompletionData
    {
        public class SectionInfo
        {
            public int docOffset;
            public string text;
        }

        public CompletionCategory CompletionCategory { get; set; }
        public string CompletionText { get; set; }
        public string Description { get; set; }
        public DisplayFlags DisplayFlags { get; set; }
        public CompletionType CompletionType { get; set; }
        public string DisplayText { get; set; }
        public bool HasOverloads { get; set; }
        public IEnumerable<ICompletionData> OverloadedData { get { return new ICompletionData[0]; } }
        public IEnumerable<string> InvokeParameters { get { return null; } }
        public string InvokeReturn { get; set; }
        public bool InvokeParametersSet { get; set; }
        public string OperationContext { get; set; }
        public object Tag { get; set; }
        public object RawData { get; set; }

        public void AddOverload(ICompletionData data)
        {
        }

        static public Dictionary<string, SectionInfo> help_map = new Dictionary<string, SectionInfo>();

        static CssCompletionData()
        {
            var help_file = CSScriptHelper.GetCSSHelp();
            if (File.Exists(help_file))
            {
                var text = File.ReadAllText(help_file);

                int prevIndex = -1;

                foreach (Match match in Regex.Matches(text, "(\r+|\n+)//css_"))
                {
                    if (prevIndex != -1)
                    {
                        // sections are terminated by "-------------------" lines
                        var section = text.Substring(prevIndex, match.Index - prevIndex).TrimEnd().TrimEnd('-');
                        var section_lines = Regex.Split(section, "\r\n|\r|\n");

                        var id = section_lines[0].Split(' ').First(); // '//css_include <file>;'

                        var alt_id = section_lines.Where(x => x.StartsWith("Alias - //css"))
                                                  .Select(x => x.Replace("Alias - ", "").Trim())
                                                  .FirstOrDefault();

                        help_map[id] = new SectionInfo { docOffset = prevIndex, text = section };
                        if (alt_id != null)
                            help_map[alt_id] = help_map[id];
                    }

                    prevIndex = match.Index + 1; // +1 because of the '/n' or '/n' prefix
                }

                AllDirectives = help_map.Select(x =>
                                            new CssCompletionData
                                            {
                                                CompletionText = x.Key,
                                                DisplayText = x.Key,
                                                Description = x.Value.text
                                            })
                                            .ToArray();
            }
        }

        public static ICompletionData[] AllDirectives =
        new ICompletionData[]
        {
            //css_import <file>[, preserve_main][, rename_namespace(<oldName>, <newName>)];
            new CssCompletionData
            {
                CompletionText = "//css_inc", DisplayText="//css_inc",
                Description =
@"'Include/Import script' directive. Can be relative or absolute path. Can also contain environment variables.
//css_inc <file>;
//css_include <file>;

Example:
    //css_inc utils.cs;"
            },

            new CssCompletionData
            {
                CompletionText = "//css_ref", DisplayText="//css_ref",
                Description =
@"'Reference assembly' directive. Use this directive to reference GAC or local assembly. Can be relative or absolute path. Can also contain environment variables.
//css_ref <file>;
//css_reference <file>;

Example:
    //css_ref System.Data.ComponentModel.dll;"
            },

            new CssCompletionData
            {
                CompletionText = "//css_args", DisplayText="//css_args",
                Description =
@"'Set command-line arguments' directive. In some way it's similar shebang but without the loader.
//css_args arg0[,arg1]..[,argN];

Example:
    //css_args /dbg, /ac, ""argument one"";"
            },

            new CssCompletionData
            {
                CompletionText = "//css_co", DisplayText="//css_co",
                Description =
@"'Set compiler options' directive. Use this directive to pass some compilation specific options directly to the compiler.
//css_co <options>;

Example:
    //css_co /d:TRACE  (to pass /d:TRACE option to C# compiler)
    //css_co /unsafe  (to allow unsafe code)"
            },
    //css_co /platform:x86 to produce Win32 executable

            new CssCompletionData
            {
                CompletionText = "//css_dir", DisplayText="//css_dir",
                Description =
@"'Set probing directory' directive. This directive is used to extend the default set of search/probing directories for dependency scripts and referenced assemblies.
//css_dir <path>;
//css_searchdir <path>;

Examples:
    //css_dir ..\\..\\MyAssemblies;
    //css_dir packages\\**"
            },

            new CssCompletionData {
                CompletionText = "//css_nuget", DisplayText="//css_nuget",
                Description =
@"'Reference NuGet package' directive. This directive not only download the package but also references its assembly(s)
//css_nuget [-noref] [-force[:delay]] [-ver:<version>] [-ng:<nuget arguments>] package0[..[,packageN];

Examples:
    //css_nuget cs-script;
    //css_nuget -ver:4.1.2 NLog;
    //css_nuget -ver:""4.1.1-rc1"" -ng:""-Pre -NoCache"" NLog;"
            },
        };

        public CssCompletionData()
        {
            CompletionType = CompletionType.directive;
        }
    }
}