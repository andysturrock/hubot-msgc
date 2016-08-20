using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Hubot_MSGroupChatAdapterService
{
    public class Hubot
    {
        private readonly Uri _hubotUri;
        private ClientWebSocket _clientWebSocket = new ClientWebSocket();
        private readonly int receiveChunkSize = 1024;

        public Hubot(Uri hubotUri)
        {
            _hubotUri = hubotUri;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _clientWebSocket = new ClientWebSocket();
            await _clientWebSocket.ConnectAsync(_hubotUri, cancellationToken);
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Adapter service stopping",
                            cancellationToken);
        }

        public async Task SendAsync(TextMessage textMessage, CancellationToken cancellationToken)
        {
            var stringMessage = JsonConvert.SerializeObject(textMessage);
            var sendBytes = Encoding.UTF8.GetBytes(stringMessage);
            var sendBuffer = new ArraySegment<byte>(sendBytes);

            await _clientWebSocket.SendAsync(
                sendBuffer,
                WebSocketMessageType.Text, true, cancellationToken);
        }

        public event EventHandler<TextMessageReceivedEventArgs> TextMessageReceived;

        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (_clientWebSocket.State == WebSocketState.Open)
            {
                var buffer = new byte[receiveChunkSize];
                WebSocketReceiveResult result;
                var stringResult = new StringBuilder();
                do
                {
                    result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await
                            _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty,
                                CancellationToken.None);
                    }
                    else
                    {
                        var str = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        stringResult.Append(str);
                    }
                } while (!result.EndOfMessage);
                var textMessage = JsonConvert.DeserializeObject<TextMessage>(stringResult.ToString());
                OnTextMessageReceived(new TextMessageReceivedEventArgs(textMessage));
            }
            //TODO provide ConnectionClosed event handler
        }

        private void OnTextMessageReceived(TextMessageReceivedEventArgs e)
        {
            TextMessageReceived?.Invoke(this, e);
        }
    }
}
