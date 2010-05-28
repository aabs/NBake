using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NBake.Properties;

namespace NBake
{
    internal class Program
    {
        private static FileSystemWatcher Watcher { get; set; }
        private static bool IsDirty { get; set; }
        private static DateTime? TimeOfLastEvent { get; set; }
        private static Timer InactivityCheckTimer { get; set; }

        private static void Main()
        {
            Initialize();
            SetupFsWatchers();
            Console.WriteLine("Press any key to halt NBake");
            Console.ReadKey();
        }

        private static void Initialize()
        {
            Environment.CurrentDirectory = Settings.Default.PathToMonitor;
            var currentPath = new DirectoryInfo(Settings.Default.PathToMonitor);
            if (!currentPath.GetDirectories().Any(di => di.Name.Equals(".git")))
            {
                RunGitCommand("init");
            }
            SetupInactivityTimer();
        }

        private static void SetupFsWatchers()
        {
            Watcher = new FileSystemWatcher(Settings.Default.PathToMonitor);
            Watcher.Changed += SomethingChanged;
            Watcher.Created += SomethingChanged;
            Watcher.Deleted += SomethingChanged;
            Watcher.Renamed += SomethingChangedName;
            Watcher.EnableRaisingEvents = true;
        }

        private static void SomethingChangedName(object sender, RenamedEventArgs e)
        {
            IsDirty = true;
            TimeOfLastEvent = DateTime.Now;
        }

        private static void SomethingChanged(object sender, FileSystemEventArgs e)
        {
            IsDirty = true;
            TimeOfLastEvent = DateTime.Now;
        }

        private static void RunGitCommand(string args)
        {
            var psi = new ProcessStartInfo(Settings.Default.gitPath, args);
            Process ps = Process.Start(psi);
            WaitForCompletion(ps, TimeSpan.FromSeconds(5));
        }

        private static void WaitForCompletion(Process ps, TimeSpan timeout)
        {
            DateTime startOfMonitoring = DateTime.Now;
            while (!ps.HasExited && DateTime.Now > startOfMonitoring.Add(timeout))
            {
                Thread.Sleep(1000);
            }
        }

        private static void Checkin()
        {
            RunGitCommand("add .");
            RunGitCommand("commit -a -m \"NBake commit\"");
        }

        private static void SetupInactivityTimer()
        {
            InactivityCheckTimer = new Timer(CheckWhetherSafeToCheckin, null, 10000, 10000);
        }

        private static TimeSpan TimeSinceLastEvent()
        {
            return DateTime.Now.Subtract(TimeOfLastEvent ?? DateTime.Now);
        }

        public static void CheckWhetherSafeToCheckin(object state)
        {
            if (IsDirty && (TimeSinceLastEvent() > TimeSpan.FromSeconds(10)))
            {
                Checkin();
                IsDirty = false;
            }
        }
    }
}