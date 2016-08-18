using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
