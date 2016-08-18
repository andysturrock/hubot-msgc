using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Hubot_MSGroupChatAdapterService
{
    public partial class Hubot_MSGroupChatAdapterService : ServiceBase
    {
        private readonly Uri _userSipUri;
        private readonly CancellationToken _cancellationToken;
        private readonly EventLog _eventLog;
        private readonly GroupChat _groupChat;
        private readonly Hubot _hubot;

        public Hubot_MSGroupChatAdapterService()
        {
            InitializeComponent();

            _cancellationToken = new CancellationTokenSource().Token;

            _eventLog = new EventLog("Hubot_MSGroupChatAdapterServiceLog");
            ((ISupportInitialize)(_eventLog)).BeginInit();
            _eventLog.Source = "Hubot_MSGroupChatAdapterService";
            _eventLog.Log = "";  // automatch to event source
            if (!EventLog.SourceExists("Hubot_MSGroupChatAdapterService"))
            {
                EventLog.CreateEventSource("Hubot_MSGroupChatAdapterService", "Application");
            }
            ((ISupportInitialize)(_eventLog)).EndInit();
            
            var hubotUri = new Uri(ConfigurationManager.AppSettings["HubotUri"]);
            _userSipUri = new Uri(ConfigurationManager.AppSettings["UserSipUri"]);
            var ocsServer = ConfigurationManager.AppSettings["OcsServer"];
            var ocsUsername = ConfigurationManager.AppSettings["OcsUsername"];
            // If we can't find the password in the registry, set to "default" and fail later.
            var password = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\hubot-msgc", "password", "default");
            var ocsPassword = password?.ToString() ?? "default";
            if (ocsPassword.Equals("default"))
            {
                _eventLog.WriteEntry(@"Failed to find password key in HKEY_LOCAL_MACHINE\SOFTWARE\hubot-msg");
            }
            ocsPassword = @"p@ssw0rd";
            var lookupServerUri = new Uri(ConfigurationManager.AppSettings["LookupServerUri"]);
            var chatRoomName = ConfigurationManager.AppSettings["ChatRoomName"];

            _groupChat = new GroupChat(_eventLog, _userSipUri, ocsServer, ocsUsername, ocsPassword, lookupServerUri, chatRoomName);

            _hubot = new Hubot(_eventLog, hubotUri);
        }

        public void OnStartPublic(string[] args)
        {
            OnStart(args);
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            _eventLog.WriteEntry("OnStart");

            try
            {
                _hubot.ConnectAsync(_cancellationToken).Wait(_cancellationToken);
                _hubot.TextMessageReceived += HubotTextMessageReceived;
                Task.Run(() => _hubot.ListenAsync(_cancellationToken), _cancellationToken);
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception while connecting to Hubot: " + exception.Message, EventLogEntryType.Error);
            }

            try
            {
                _groupChat.TextMessageReceived += GroupChatTextMessageReceivedAsync;
                _groupChat.Connect();
                _groupChat.Send("Connected to GroupChat and Hubot.");
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception connecting to GroupChat: " + exception.Message, EventLogEntryType.Error);
            }
            _eventLog.WriteEntry("Connected to GroupChat and Hubot.");
        }

        protected override void OnStop()
        {
            base.OnStop();
            _eventLog.WriteEntry("OnStop");

            try
            {
                // Leave the room.
                // TODO send Hubot room leave message
                _groupChat.Disconnect();
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception disconnecting from GroupChat: " + exception.Message, EventLogEntryType.Error);
            }

            try
            {
                _hubot.DisconnectAsync(_cancellationToken).Wait(_cancellationToken);
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception disconnecting from Hubot: " + exception.Message, EventLogEntryType.Error);
            }
        }

        private void HubotTextMessageReceived(object sender, TextMessageReceivedEventArgs e)
        {
            _groupChat.Send(e.TextMessage.Text);
        }

        private async void GroupChatTextMessageReceivedAsync(object sender, TextMessageReceivedEventArgs e)
        {
            // Don't send messages from Hubot back to itself...
            var userSipUri = new Uri(e.TextMessage.UserName);
            if (!userSipUri.Equals(_userSipUri))
            {
                await _hubot.SendAsync(e.TextMessage, _cancellationToken);
            }
        }
    }
}
