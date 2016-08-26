using System;

namespace Hubot_MSGroupChatAdapterService
{
    public class DisconnectedEventArgs : EventArgs
    {
        public DisconnectedEventArgs(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; set; }
    }
}
