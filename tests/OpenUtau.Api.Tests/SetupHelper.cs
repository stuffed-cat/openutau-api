using System.Reflection;
using System.Threading;
using OpenUtau.Core;
using Serilog;
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
            
            // Provide a mock UI thread dispatcher in case it's still called
            DocManager.Inst.PostOnUIThread = action => action();

            // Set Log to prevent Serilog errors
            if (Serilog.Log.Logger == null || Serilog.Log.Logger.GetType().Name == "SilentLogger")
            {
                Serilog.Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
            }
        }

        public static void SetProject(UProject project)
        {
            var prop = typeof(DocManager).GetProperty("Project");
            prop.DeclaringType.GetProperty("Project").GetSetMethod(true).Invoke(DocManager.Inst, new object[] { project });
        }
    }
}
