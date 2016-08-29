using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

namespace Hubot_MSGroupChatAdapterService
{
    public class Hubot
    {
        private readonly Uri _hubotUri;
        private ClientWebSocket _clientWebSocket;
        // TODO should probably be configurable
        private const int ReceiveChunkSize = 1024;

        public Hubot(Uri hubotUri)
        {
            _hubotUri = hubotUri;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _clientWebSocket = new ClientWebSocket();
            await _clientWebSocket.ConnectAsync(_hubotUri, cancellationToken);
            Connected = true;
        }

        public bool Connected { get; private set; }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Adapter service stopping",
                            cancellationToken);
            Connected = false;
        }

        public async Task SendAsync(TextMessage textMessage, CancellationToken cancellationToken)
        {
            var stringMessage = JsonConvert.SerializeObject(textMessage);
            var sendBytes = Encoding.UTF8.GetBytes(stringMessage);
            var sendBuffer = new ArraySegment<byte>(sendBytes);

            try
            {
                await _clientWebSocket.SendAsync(
                    sendBuffer,
                    WebSocketMessageType.Text, true, cancellationToken);
            }
            catch (Exception e)
            {
                OnDisconnected(new DisconnectedEventArgs("Server disconnected: " + e));
            }
        }

        public event EventHandler<TextMessageReceivedEventArgs> TextMessageReceived;

        public event EventHandler<DisconnectedEventArgs> Disconnected;

        private void OnDisconnected(DisconnectedEventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }

        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (_clientWebSocket.State == WebSocketState.Open)
            {
                var buffer = new byte[ReceiveChunkSize];
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
            Connected = false;
            OnDisconnected(new DisconnectedEventArgs($"Websocket closed.  Status: {_clientWebSocket.CloseStatusDescription}"));
        }

        private void OnTextMessageReceived(TextMessageReceivedEventArgs e)
        {
            TextMessageReceived?.Invoke(this, e);
        }
    }
}
