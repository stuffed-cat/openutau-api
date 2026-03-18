using System.Reflection;
using System.Threading;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Tests
{
    public static class SetupHelper
    {
        public static void InitDocManager()
        {
            // Set mainThread to current thread so ExecuteCmd doesn't jump to PostOnUIThread
            var field = typeof(DocManager).GetField("mainThread", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(DocManager.Inst, Thread.CurrentThread);
            
            // Set Log to prevent Serilog errors just in case
            Serilog.Log.Logger = new Serilog.LoggerConfiguration().CreateLogger();
        }

        public static void SetProject(UProject project)
        {
            var prop = typeof(DocManager).GetProperty("Project");
            prop.DeclaringType.GetProperty("Project").GetSetMethod(true).Invoke(DocManager.Inst, new object[] { project });
        }
    }
}
