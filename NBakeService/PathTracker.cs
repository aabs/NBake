using System;
using System.IO;
using System.Threading;

namespace NBakeService
{
    public class PathTracker
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
}