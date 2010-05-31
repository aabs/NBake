using System;
using System.Collections.Generic;

namespace NBakeService.Config
{
    public class Target
    {
        public string Path { get; set; }
        public Dictionary<string, Repository> Remotes { get; set; }
        public Dictionary<string, string> Settings { get; set; }
    }

    public class Repository
    {
        public string Name { get; set; }
        public bool AutoPush { get; set; }
        public Uri RepoUri { get; set; }
    }
}