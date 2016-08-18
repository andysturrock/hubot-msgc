using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Hubot_MSGroupChatAdapterService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] servicesToRun = new ServiceBase[]
            {
                new Hubot_MSGroupChatAdapterService(@"ws://172.31.40.209:4773")
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
