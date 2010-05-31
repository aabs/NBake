using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NBakeService.Config
{
    public class Configuration
    {
        public static Dictionary<string, string> GetGlobalSettings(XDocument doc)
        {
            return ReadPropertyCollection(doc.Element("Properties"));
        }

        private static Dictionary<string, string> ReadPropertyCollection(XElement xElement)
        {
            if (xElement == null || xElement.Elements("property") == null || xElement.Elements("property").Count() == 0)
            {
                return new Dictionary<string, string>();
            }

            return xElement.Elements("property").ToDictionary(
                e => e.Attribute("name").Value,
                e => e.Attribute("value").Value);
        }

        public static IEnumerable<Target> GetAllTargets(XDocument doc)
        {
            foreach (XElement t in doc.Element("nbake").Element("targets").Elements("target"))
            {
                yield return new Target
                                 {
                                     Path = t.Element("path").Value,
                                     Remotes = ReadRemoteRepos(t),
                                     Settings = ReadPropertyCollection(t)
                                 };
            }
        }

        private static Dictionary<string, Repository> ReadRemoteRepos(XElement t)
        {
            return t.Element("remoteRepositories").Elements("repository").ToDictionary(
                r => r.Attribute("name").Value,
                r => new Repository
                         {
                             AutoPush = Convert.ToBoolean(r.Attribute("autopush").Value),
                             Name = r.Attribute("name").Value,
                             RepoUri = new Uri(r.Attribute("uri").Value)
                         });
        }
    }
}