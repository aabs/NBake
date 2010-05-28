using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using NBakeService.Properties;
using System.Collections.Generic;

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

        private void Checkin(string path)
        {
            RunGitCommand("add .", path);
            RunGitCommand("commit -a -m \"NBake commit\"", path);
        }

        public void CheckWhetherSafeToCheckin(object state)
        {
            var tracker = state as PathTracker;
            if (tracker == null)
                return;
            if (tracker.IsDirty && (tracker.TimeSinceLastEvent() > TimeSpan.FromSeconds(10)))
            {
                Checkin(tracker.Path);
                tracker.IsDirty = false;
            }
        }

        private void InitializeWatchers()
        {
            var watchedPaths = Settings.Default.PathToMonitor.Split(';');
            foreach (var watchedPath in watchedPaths)
            {
                if (!WatchedDirectoryIsUnderRevisionControl(watchedPath))
                {
                    SetupRepositoryInWatchedDirectory(watchedPath);
                }
                var watcher = new FileSystemWatcher(watchedPath);
                watcher.Changed += SomethingChanged;
                watcher.Created += SomethingChanged;
                watcher.Deleted += SomethingChanged;
                watcher.Renamed += SomethingChangedName;
                watcher.EnableRaisingEvents = true;
                var tracker = new PathTracker
                {
                    Watcher = watcher,
                    Path = watchedPath,
                    TimeOfLastEvent = null,
                    IsDirty = false
                };
                tracker.InactivityCheckTimer = new Timer(CheckWhetherSafeToCheckin, tracker, 10000, 10000);
                Trackers[watchedPath] = tracker;
            }
        }

        protected override void OnStart(string[] args)
        {
            InitializeWatchers();
            SetupFsWatchers();
        }

        protected override void OnStop()
        {
            foreach (var t in Trackers.Values)
            {
                if (t.InactivityCheckTimer != null)
                {
                    t.InactivityCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                t.Watcher.EnableRaisingEvents = false;
                if (t.IsDirty)
                {
                    Checkin(t.Path);
                }
            }
        }

        static object locker = new object();
        private void RunGitCommand(string args, string path)
        {
            lock (locker)
            {
                Environment.CurrentDirectory = path;
                var psi = new ProcessStartInfo(Settings.Default.gitPath, args);
                Process ps = Process.Start(psi);
                WaitForCompletion(ps, TimeSpan.FromSeconds(5));
            }
        }

        class PathTracker
        {
            public bool IsDirty { get; set; }
            public DateTime? TimeOfLastEvent { get; set; }
            public Timer InactivityCheckTimer { get; set; }
            public string Path { get; set; }
            public FileSystemWatcher Watcher { get; set; }
            public TimeSpan TimeSinceLastEvent()
            {
                return DateTime.Now.Subtract(TimeOfLastEvent ?? DateTime.Now);
            }
        }

        Dictionary<string, PathTracker> Trackers = new Dictionary<string, PathTracker>();
        
        private void SetupFsWatchers()
        {
            var watchedPaths = Settings.Default.PathToMonitor.Split(';');
            foreach (var path in watchedPaths)
            {
            }
        }

        private void SetupRepositoryInWatchedDirectory(string path)
        {
            RunGitCommand("init", path);

            // setup local user and email credentials
            RunGitCommand("config --global user.name \"" + Settings.Default.userId + "\"", path);
            RunGitCommand("config --global user.email \"" + Settings.Default.email + "\"", path);

            // define .gitignore file in the watched directory
            var ignores = Settings.Default.IgnorePatterns.Split(',');
            var ignoreFile = new FileInfo(Path.Combine(path, ".gitignore"));
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
            var fsw = sender as FileSystemWatcher;
            TrackChange(fsw, e.ChangeType);
        }

        void TrackChange(FileSystemWatcher fsw, WatcherChangeTypes changeType)
        {
            var t = Trackers[fsw.Path];
            Log("Something was {0} in {1}", Enum.GetName(typeof(WatcherChangeTypes), changeType), fsw.Path);
            t.IsDirty = true;
            t.TimeOfLastEvent = DateTime.Now;
        }

        void Log(string msg, params object[] args)
        {
            Debug.WriteLine(string.Format(msg, args));
        }

        private void SomethingChangedName(object sender, RenamedEventArgs e)
        {
            var fsw = sender as FileSystemWatcher;
            TrackChange(fsw, e.ChangeType);
        }

        private void WaitForCompletion(Process ps, TimeSpan timeout)
        {
            DateTime startOfMonitoring = DateTime.Now;
            while (!ps.HasExited && DateTime.Now > startOfMonitoring.Add(timeout))
            {
                Thread.Sleep(1000);
            }
        }

        private bool WatchedDirectoryIsUnderRevisionControl(string path)
        {
            var currentPath = new DirectoryInfo(path);
            return (currentPath.GetDirectories().Any(di => di.Name.Equals(".git")));
        }
    }
}