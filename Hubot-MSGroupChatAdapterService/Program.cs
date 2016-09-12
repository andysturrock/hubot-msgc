using System.ServiceProcess;
using System.Threading;

namespace Hubot_MSGroupChatAdapterService
{
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var servicesToRun = new ServiceBase[]
            {
                new HubotMsGroupChatAdapterService()
            };
            ServiceBase.Run(servicesToRun);

            // To run from command line/in debugger, comment out lines above and uncomment these lines...

            //AutoResetEvent shutdownEvent = new AutoResetEvent(false);
            //var hubotMsGroupChatAdapterService = new HubotMsGroupChatAdapterService();
            //hubotMsGroupChatAdapterService.OnStartPublic(null);
            //shutdownEvent.WaitOne();
        }
    }
}
