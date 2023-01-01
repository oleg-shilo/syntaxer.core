using Syntaxer;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using static Global;

namespace syntaxer.core.tests
{
    public class ProjectTest
    {
        [Fact]
        public static void EmptyProject()
        {
            WithDisposable(script =>
            {
                Project project = CSScriptHelper.GenerateProjectFor(new SourceInfo(script));

                Assert.NotEmpty(project.Refs);
                Assert.NotEmpty(project.SearchDirs);
                Assert.NotEmpty(project.Files);
                Assert.Equal(project.Files.First(), script);
            });
        }

        [Fact]
        public static void WithMultiRefsProject() => WithDisposable(script =>
        {
            var search_dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            File.WriteAllText(script, $@"
                    //css_dir {search_dir}
                    //css_ref {CSScriptProxy.cscs_path}
                    //css_ref {Assembly.GetExecutingAssembly().Location}");

            Project project = CSScriptHelper.GenerateProjectFor(new SourceInfo(script));

            Assert.NotEmpty(project.Files);
            Assert.Equal(project.Files.First(), script);
            Assert.Equal(project.Files.First(), script);
            Assert.Contains(CSScriptProxy.cscs_path, project.Refs);
            Assert.Contains(Assembly.GetExecutingAssembly().Location, project.Refs);
            Assert.Contains(search_dir, project.SearchDirs);
        });
    }
}