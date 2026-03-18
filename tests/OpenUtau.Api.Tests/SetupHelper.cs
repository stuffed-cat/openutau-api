using System;
using System.Reflection;
using System.Threading;
using OpenUtau.Core;
using Serilog;
using OpenUtau.Core.Ustx;
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

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
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                // Force DocManager to use current thread as main thread whenever called!
                var field = typeof(DocManager).GetField("mainThread", BindingFlags.Instance | BindingFlags.NonPublic);
                field?.SetValue(DocManager.Inst, Thread.CurrentThread);
                DocManager.Inst.Initialize(Thread.CurrentThread, System.Threading.Tasks.TaskScheduler.Default);

                if (_initialized) return;

                // Stop ExecuteCmd completely from calling PostOnUIThread for things it doesn't recognize
                // Instead of a do-nothing stub, actually execute the action as if it's the UI thread dispatcher
                DocManager.Inst.PostOnUIThread = action => 
                {
                    // Temporarily set the mainthread to current thread to avoid infinite loops
                    var currentMainThread = field?.GetValue(DocManager.Inst) as Thread;
                    field?.SetValue(DocManager.Inst, Thread.CurrentThread);
                DocManager.Inst.Initialize(Thread.CurrentThread, System.Threading.Tasks.TaskScheduler.Default);
                    try 
                    {
                        // Ensure action runs genuinely synchronously
                        action();
                    }
                    finally 
                    {
                        // Restore
                        field?.SetValue(DocManager.Inst, currentMainThread);
                    }
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

