using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace NBakeService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (args != null && args.Length == 1 && args[0] == "/console")
            {
                var ep = new NBakeService();
                ep.RunInConsoleMode(args);
                Console.WriteLine("Press any key to finish.");
                Console.Read();
                return;
            }

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new NBakeService() 
			};
            ServiceBase.Run(ServicesToRun);
        }
    }
}
