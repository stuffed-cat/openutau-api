using System;
using System.IO;
using System.Reflection;
using System.Threading;
using OpenUtau.Core;
using Serilog;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;

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
                var field = typeof(DocManager).GetField("mainThread", BindingFlags.Instance | BindingFlags.NonPublic);
                field?.SetValue(DocManager.Inst, Thread.CurrentThread);
                DocManager.Inst.Initialize(Thread.CurrentThread, System.Threading.Tasks.TaskScheduler.Default);
                
                if (_initialized) return;

                DocManager.Inst.PostOnUIThread = action => 
                {
                    var currentMainThread = field?.GetValue(DocManager.Inst) as Thread;
                    field?.SetValue(DocManager.Inst, Thread.CurrentThread);
                    DocManager.Inst.Initialize(Thread.CurrentThread, System.Threading.Tasks.TaskScheduler.Default);
                    try 
                    {
                        action();
                    }
                    finally 
                    {
                        field?.SetValue(DocManager.Inst, currentMainThread);
                    }
                };

                if (Serilog.Log.Logger == null || Serilog.Log.Logger.GetType().Name == "SilentLogger")
                {
                    Serilog.Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
                }

                _initialized = true;
            }
        }

        public static void CreateAndLoadRealProject(System.Action<UProject> builder = null)
        {
            var project = OpenUtau.Core.Format.Ustx.Create();
            
            if (builder != null) {
                builder(project);
            }

            string tempFile = Path.GetTempFileName() + ".ustx";
            OpenUtau.Core.Format.Ustx.Save(tempFile, project);
            Formats.LoadProject(new string[] { tempFile });
            System.IO.File.Delete(tempFile);
        }
        
    }
}
