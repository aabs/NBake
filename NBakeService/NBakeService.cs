using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using NBakeService.Properties;

namespace NBakeService
{
    public partial class NBakeService : ServiceBase
    {
        public NBakeService()
        {
            InitializeComponent();
        }
        public void RunInConsoleMode(string[] args)
        {
            OnStart(args);
        }

        private Timer InactivityCheckTimer { get; set; }
        private bool IsDirty { get; set; }
        private DateTime? TimeOfLastEvent { get; set; }
        private FileSystemWatcher Watcher { get; set; }

        private void Checkin()
        {
            RunGitCommand("add .");
            RunGitCommand("commit -a -m \"NBake commit\"");
        }

        public void CheckWhetherSafeToCheckin(object state)
        {
            if (IsDirty && (TimeSinceLastEvent() > TimeSpan.FromSeconds(10)))
            {
                Checkin();
                IsDirty = false;
            }
        }

        private void InitializeWatchers()
        {
            Environment.CurrentDirectory = Settings.Default.PathToMonitor;
            var currentPath = new DirectoryInfo(Settings.Default.PathToMonitor);
            if (WatchedDirectoryIsUnderRevisionControl())
            {
                SetupRepositoryInWatchedDirectory();
            }
            SetupInactivityTimer();
        }

        protected override void OnStart(string[] args)
        {
            InitializeWatchers();
            SetupFsWatchers();
        }

        protected override void OnStop()
        {
            if (InactivityCheckTimer != null)
            {
                InactivityCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            Watcher.EnableRaisingEvents = false;
            if (IsDirty)
            {
                Checkin();
            }
        }

        private void RunGitCommand(string args)
        {
            var psi = new ProcessStartInfo(Settings.Default.gitPath, args);
            Process ps = Process.Start(psi);
            WaitForCompletion(ps, TimeSpan.FromSeconds(5));
        }

        private void SetupFsWatchers()
        {
            Watcher = new FileSystemWatcher(Settings.Default.PathToMonitor);
            Watcher.Changed += SomethingChanged;
            Watcher.Created += SomethingChanged;
            Watcher.Deleted += SomethingChanged;
            Watcher.Renamed += SomethingChangedName;
            Watcher.EnableRaisingEvents = true;
        }

        private void SetupInactivityTimer()
        {
            InactivityCheckTimer = new Timer(CheckWhetherSafeToCheckin, null, 10000, 10000);
        }

        private void SetupRepositoryInWatchedDirectory()
        {
            RunGitCommand("init");

            // setup local user and email credentials
            RunGitCommand("config --global user.name \"" + Settings.Default.userId + "\"");
            RunGitCommand("config --global user.email \"" + Settings.Default.email + "\"");

            // define .gitignore file in the watched directory
            var ignores = Settings.Default.IgnorePatterns.Split(',');
            var ignoreFile = new FileInfo(Path.Combine(Settings.Default.PathToMonitor, ".gitignore"));
            if (!ignoreFile.Exists)
            {
                using(var stream = ignoreFile.Open(FileMode.Create, FileAccess.Write, FileShare.None))
                using(var writer = new StreamWriter(stream))
                {
                    foreach (var s in ignores)
                    {
                        writer.WriteLine(s);
                    }
                }
            }
        }

        private void SomethingChanged(object sender, FileSystemEventArgs e)
        {
            IsDirty = true;
            TimeOfLastEvent = DateTime.Now;
        }

        private void SomethingChangedName(object sender, RenamedEventArgs e)
        {
            IsDirty = true;
            TimeOfLastEvent = DateTime.Now;
        }

        private TimeSpan TimeSinceLastEvent()
        {
            return DateTime.Now.Subtract(TimeOfLastEvent ?? DateTime.Now);
        }

        private void WaitForCompletion(Process ps, TimeSpan timeout)
        {
            DateTime startOfMonitoring = DateTime.Now;
            while (!ps.HasExited && DateTime.Now > startOfMonitoring.Add(timeout))
            {
                Thread.Sleep(1000);
            }
        }

        private bool WatchedDirectoryIsUnderRevisionControl()
        {
            var currentPath = new DirectoryInfo(Settings.Default.PathToMonitor);
            return (!currentPath.GetDirectories().Any(di => di.Name.Equals(".git")));
        }
    }
}