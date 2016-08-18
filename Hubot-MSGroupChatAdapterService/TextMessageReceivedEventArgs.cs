using System;

namespace Hubot_MSGroupChatAdapterService
{
    public class TextMessageReceivedEventArgs : EventArgs
    {
        public TextMessageReceivedEventArgs(TextMessage textMessage)
        {
            TextMessage = textMessage;
        }

        public TextMessage TextMessage { get; set; }
    }
}
