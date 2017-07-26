using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace CK.ControlChannel.Tcp.Tests
{
    [CollectionDefinition( "Main collection" )]
    public class MainTestCollection : ICollectionFixture<MainTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class MainTestFixture : IDisposable
    {
        public MainTestFixture()
        {
            SetupActivityMonitor();
        }

        private void SetupActivityMonitor()
        {
            Console.OutputEncoding = Encoding.UTF8;
            SystemActivityMonitor.RootLogPath = GetTestLogDirectory();
            ActivityMonitor.DefaultFilter = LogFilter.Debug;
            ActivityMonitor.AutoConfiguration += ( monitor ) =>
            {
                monitor.Output.RegisterClient( new ActivityMonitorConsoleClient() );
            };
            GrandOutputConfiguration grandOutputConfig = new GrandOutputConfiguration();
            grandOutputConfig.AddHandler( new TextFileConfiguration()
            {
                MaxCountPerFile = 10000,
                Path = "Text",
            } );
            GrandOutput.EnsureActiveDefault( grandOutputConfig );
        }

        public void Dispose()
        {
            GrandOutput.Default.Dispose();
        }

        static string GetTestLogDirectory()
        {
            var dllPath = typeof( MainTestFixture ).GetTypeInfo().Assembly.Location;
            var dllDir = Path.GetDirectoryName( dllPath );
            var logPath = Path.Combine( dllDir, "Logs" );
            if( !Directory.Exists( logPath ) )
            {
                Directory.CreateDirectory( logPath );
            }
            return logPath;
        }

    }
}
