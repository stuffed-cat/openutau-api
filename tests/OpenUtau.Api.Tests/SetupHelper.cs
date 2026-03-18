using System;
using System.Reflection;
using System.Threading;
using OpenUtau.Core;
using Serilog;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Tests
{
    public static class SetupHelper
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        public static void InitDocManager()
        {
            lock (_lock)
            {
                // Force DocManager to use current thread as main thread whenever called!
                var field = typeof(DocManager).GetField("mainThread", BindingFlags.Instance | BindingFlags.NonPublic);
                field?.SetValue(DocManager.Inst, Thread.CurrentThread);

                if (_initialized) return;

                // Stop ExecuteCmd completely from calling PostOnUIThread for things it doesn't recognize
                DocManager.Inst.PostOnUIThread = action => 
                {
                    // Do literally nothing! Or execute directly.
                    // Doing nothing stops the infinite loop.
                };

                // Set Log to prevent Serilog errors
                if (Serilog.Log.Logger == null || Serilog.Log.Logger.GetType().Name == "SilentLogger")
                {
                    Serilog.Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
                }

                _initialized = true;
            }
        }

        public static void SetProject(UProject project)
        {
            var prop = typeof(DocManager).GetProperty("Project");
            prop.DeclaringType.GetProperty("Project").GetSetMethod(true).Invoke(DocManager.Inst, new object[] { project });
        }
    }
}
