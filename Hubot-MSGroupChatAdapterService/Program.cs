using System.ServiceProcess;

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
                new Hubot_MSGroupChatAdapterService()
            };
            ServiceBase.Run(servicesToRun);

            // To run from command line/in debugger, comment out lines above and uncomment these lines...

            //AutoResetEvent shutdownEvent = new AutoResetEvent(false);
            //var hubot_MSGroupChatAdapterService = new Hubot_MSGroupChatAdapterService(@"ws://172.31.40.209:4773");
            //hubot_MSGroupChatAdapterService.OnStartPublic(null);
            //shutdownEvent.WaitOne();
        }
    }
}
