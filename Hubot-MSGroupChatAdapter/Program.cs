using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Transports;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Hubot_MSGroupChatAdapter
{
    internal class Program
    {
        //private static readonly string serverURI = @"http://192.168.10.12:4773/";
        private static readonly string serverURI = @"http://172.31.40.209:4773";

        public static void Main2(string[] args)
        {
            var connection = new HubConnection(serverURI);
            connection.TraceLevel = TraceLevels.All;
            connection.TraceWriter = Console.Out;
            connection.Closed += Connection_Closed;
            var hubProxy = connection.CreateHubProxy("");

            hubProxy.On<string>("message", message => Console.WriteLine($"Received message: {message}"));
            hubProxy.On("heartbeat", () => Console.Write("Received heartbeat \n"));
            ServicePointManager.DefaultConnectionLimit = 10;
            try
            {
                connection.Start(new WebSocketTransport()).Wait();
            }
            catch (HttpRequestException)
            {
                Console.Error.WriteLine("Unable to connect to server: Start server before connecting clients.");
                return;
            }
            Console.Out.WriteLine($"Connected to server at {serverURI}");

            //hubProxy.Invoke("Heartbeat").ContinueWith(task =>
            //{
            //    if (task.IsFaulted)
            //    {
            //        Console.WriteLine("Error sending heartbeat:{0}", task.Exception.GetBaseException());
            //    }

            //}).Wait();
            //Console.WriteLine("client heartbeat sent to server\n");

            hubProxy.Invoke("message", "{\"message\": \"hello world\"}").ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Console.WriteLine("Error sending message:{0} \n", task.Exception.GetBaseException());
                }
            }).Wait();
            Console.WriteLine("Client sent message to server\n");
        }

        private static void Connection_Closed()
        {
            Console.Out.WriteLine($"Connection from {serverURI} closed.");
        }

        private static async Task HandleMessageAsync(string name, string message)
        {
            await Task.Run(() => Console.Out.WriteLine($"{name}: {message}"));
        }

        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
            //Main2(args);
        }

        private static async Task MainAsync(string[] args)
        {
            var cts = new CancellationTokenSource();
            var socket = new ClientWebSocket();
            var wsUri = "ws://192.168.10.12:4773/";
            await socket.ConnectAsync(new Uri(wsUri), cts.Token);

            await Task.Run(() => StartListen(socket, cts.Token), cts.Token);

            while (true)
            {
                var message = Console.ReadLine();
                if (message == "Bye")
                {
                    cts.Cancel();
                    return;
                }
                //var textMessage =
                //    "{\"type\": \"text\", \"message_id\": 123, \"username\": \"andy\", \"room\": \"testroom\", \"text\": \"" +
                //    message + "\"}";
                var textMessage = new TextMessage("text", "123", "andy", "test_room", message);
                var stringMessage = JsonConvert.SerializeObject(textMessage);
                Console.Out.WriteLine("Sending:" + stringMessage);
                var sendBytes = Encoding.UTF8.GetBytes(stringMessage);
                var sendBuffer = new ArraySegment<byte>(sendBytes);
                await socket.SendAsync(
                    sendBuffer,
                    WebSocketMessageType.Text, true, cts.Token);
            }
        }

        private static async void StartListen(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            const int receiveChunkSize = 1024;
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var buffer = new byte[receiveChunkSize];
                    WebSocketReceiveResult result;
                    var stringResult = new StringBuilder();
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await
                                socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty,
                                    CancellationToken.None);
                        }
                        else
                        {
                            var str = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            stringResult.Append(str);
                        }
                    } while (!result.EndOfMessage);;
                    TextMessage textMessage = JsonConvert.DeserializeObject<TextMessage>(stringResult.ToString());
                    // Replace \n wth newline chars
                    //var regex = new Regex("\\\\n");
                    //var output = regex.Replace(stringResult.ToString(), System.Environment.NewLine);
                    Console.Out.WriteLine("Got: " + stringResult.ToString());
                    Console.Out.WriteLine("Parsed: " + textMessage.ToString());
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e);
            }
            finally
            {
                socket.Dispose();
            }
        }
    }

    //{"room":"testroom","user":{"id":"andy","name":"andy","room":"testroom"},"message":{"user":{"id":"andy","name":"andy","room":"testroom"},"text":"oilbot help","id":123,"done":false,"room":"testroom"}}
    //  "envelope": {
    //      "room":"testroom",
    //      "user": {
    //          "id":"andy","name":"andy","room":"testroom"
    //      },
    //      "message": {
    //          "user": {
    //              "id":"andy","name":"andy","room":"testroom"
    //      },
    //      "text":"oilbot help",
    //      "id":123,"done":false,"room":"testroom"
    //  }}

    class TextMessage
    {
        public TextMessage(string type, String id, string userName, string room, string text)
        {
            Type = type;
            Id = id;
            UserName = userName;
            Room = room;
            Text = text;
        }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("username")]
        public string UserName { get; set; }
        [JsonProperty("room")]
        public string Room { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }

        public override string ToString()
        {
            return $"Type: {Type}, Id: {Id}, UserName: {UserName}, Room: {Room}, Text: {Text}";
        }
    }
}
