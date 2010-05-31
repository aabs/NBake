using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NBakeService.Config;
using NBakeService.Properties;

namespace NBakeService
{
    public class Baker : IBaker
    {
        private static readonly object Locker = new object();
        private readonly Dictionary<string, PathTracker> _trackers = new Dictionary<string, PathTracker>();
        public Dictionary<string, string> GlobalSettings { get; set; }

        #region IBaker Members

        public void OnStart(string[] args)
        {
            string location = GetType().Assembly.Location;
            if (string.IsNullOrEmpty(location))
                throw new ApplicationException("location of service directory not found");

            string configPath = Path.Combine(Path.GetDirectoryName(location), Settings.Default.nbakeConfig);

            if (!File.Exists( configPath))
                throw new ApplicationException("unable to locate configuration settings");
            
            XDocument configXml = XDocument.Load(configPath);
            GlobalSettings = Configuration.GetGlobalSettings(configXml);
            IEnumerable<Target> targets = Configuration.GetAllTargets(configXml);
            StartMonitoring(targets);
        }

        public void OnStop()
        {
            foreach (PathTracker t in _trackers.Values)
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

        #endregion

        private void Checkin(string path)
        {
            Log("Checking in {0}", path);
            RunGitCommand("add .", path);
            RunGitCommand("commit -a -m \"NBake commit\"", path);
        }

        public void CheckWhetherSafeToCheckin(object state)
        {
            var tracker = state as PathTracker;
            if (tracker == null)
            {
                Log("Error: Tracker not found");
                return;
            }
            if (tracker.IsDirty && (tracker.TimeSinceLastEvent() > TimeSpan.FromSeconds(10)))
            {
                Checkin(tracker.Path);
                tracker.IsDirty = false;
            }
        }

        public void StartMonitoring(IEnumerable<Target> targets)
        {
            foreach (Target target in targets)
            {
                if (!WatchedDirectoryIsUnderRevisionControl(target.Path))
                {
                    SetupRepositoryInWatchedDirectory(target);
                }
                var watcher = new FileSystemWatcher(target.Path);
                watcher.Changed += SomethingChanged;
                watcher.Created += SomethingChanged;
                watcher.Deleted += SomethingChanged;
                watcher.Renamed += SomethingChangedName;
                watcher.EnableRaisingEvents = true;
                var tracker = new PathTracker
                                  {
                                      Watcher = watcher,
                                      Path = target.Path,
                                      TimeOfLastEvent = null,
                                      IsDirty = false
                                  };
                int interval = GetSetting(target, "commitCheckTimerPeriodMs", 10000);
                int duetime = interval;
                tracker.InactivityCheckTimer = new Timer(CheckWhetherSafeToCheckin, tracker, duetime, interval);
                _trackers[target.Path] = tracker;
            }
        }

        private void RunGitCommand(string args,
                                   string path)
        {
            lock (Locker)
            {
                Environment.CurrentDirectory = path;
                string gitPath = GetSetting(null, "gitPath", @"C:\Program Files\Git\bin\git.exe");
                var psi = new ProcessStartInfo(gitPath, args);
                Process ps = Process.Start(psi);
                WaitForCompletion(ps, TimeSpan.FromSeconds(5));
            }
        }


        private void SetupRepositoryInWatchedDirectory(Target t)
        {
            RunGitCommand("init", t.Path);

            // setup local user and email credentials
            RunGitCommand("config --global user.name \"" + GetSetting<string>(t, "userId") + "\"", t.Path);
            RunGitCommand("config --global user.email \"" + GetSetting<string>(t, "email") + "\"", t.Path);

            // define .gitignore file in the watched directory
            var ignoreList = GetSetting<string>(t, "ignoreList");
            string[] ignores = ignoreList.Split(',');
            var ignoreFile = new FileInfo(Path.Combine(t.Path, ".gitignore"));
            if (!ignoreFile.Exists)
            {
                using (FileStream stream = ignoreFile.Open(FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    foreach (string s in ignores)
                    {
                        writer.WriteLine(s);
                    }
                }
            }
        }

        private T GetSetting<T>(Target target,
                                string key,
                                T defaultValue = default(T))
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("key");

            try
            {
                if (target != null && target.Settings.ContainsKey(key))
                {
                    if (!string.IsNullOrEmpty(target.Settings[key]))
                        return (T)Convert.ChangeType(target.Settings[key], typeof(T));
                }
                else if (GlobalSettings != null && GlobalSettings.ContainsKey(key))
                {
                    if (!string.IsNullOrEmpty(GlobalSettings[key]))
                        return (T)Convert.ChangeType(GlobalSettings[key], typeof(T));
                }
            }
            catch
            {
                Log("Error: unable to locate configuration setting");
                return defaultValue;
            }
            return defaultValue;
        }

        private void SomethingChanged(object sender,
                                      FileSystemEventArgs e)
        {
            var fsw = sender as FileSystemWatcher;
            TrackChange(fsw, e.ChangeType);
        }

        private void TrackChange(FileSystemWatcher fsw,
                                 WatcherChangeTypes changeType)
        {
            PathTracker t = _trackers[fsw.Path];
            Log("Something was {0} in {1}", Enum.GetName(typeof(WatcherChangeTypes), changeType), fsw.Path);
            t.IsDirty = true;
            t.TimeOfLastEvent = DateTime.Now;
        }

        [Conditional("DEBUG")]
        private static void Log(string msg,
                         params object[] args)
        {
            Debug.WriteLine(string.Format(msg, args));
        }

        private void SomethingChangedName(object sender,
                                          RenamedEventArgs e)
        {
            var fsw = sender as FileSystemWatcher;
            TrackChange(fsw, e.ChangeType);
        }

        private static void WaitForCompletion(Process ps,
                                       TimeSpan timeout)
        {
            DateTime startOfMonitoring = DateTime.Now;
            while (!ps.HasExited && DateTime.Now > startOfMonitoring.Add(timeout))
            {
                Thread.Sleep(1000);
            }
        }

        private static bool WatchedDirectoryIsUnderRevisionControl(string path)
        {
            var currentPath = new DirectoryInfo(path);
            return (currentPath.GetDirectories().Any(di => di.Name.Equals(".git")));
        }
    }
}