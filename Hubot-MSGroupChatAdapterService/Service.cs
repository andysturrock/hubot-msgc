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
    public partial class HubotMsGroupChatAdapterService : ServiceBase
    {
        private readonly Uri _userSipUri;
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly EventLog _eventLog;
        private readonly GroupChat _groupChat;
        private readonly Hubot _hubot;

        public HubotMsGroupChatAdapterService()
        {
            InitializeComponent();

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            _eventLog = new EventLog("HubotMSGroupChatAdapterServiceLog");
            ((ISupportInitialize)(_eventLog)).BeginInit();
            _eventLog.Source = "HubotMSGroupChatAdapterService";
            _eventLog.Log = "";  // automatch to event source
            if (!EventLog.SourceExists("HubotMSGroupChatAdapterService"))
            {
                EventLog.CreateEventSource("HubotMSGroupChatAdapterService", "Application");
            }
            ((ISupportInitialize)(_eventLog)).EndInit();
            
            var hubotUri = new Uri(ConfigurationManager.AppSettings["HubotUri"]);
            _userSipUri = new Uri(ConfigurationManager.AppSettings["UserSipUri"]);
            var ocsServer = ConfigurationManager.AppSettings["OcsServer"];
            var ocsUsername = ConfigurationManager.AppSettings["OcsUsername"];
            var lookupServerUri = new Uri(ConfigurationManager.AppSettings["LookupServerUri"]);
            var chatRoomName = ConfigurationManager.AppSettings["ChatRoomName"];
            var useSso = Boolean.Parse(ConfigurationManager.AppSettings["UseSso"]);
            if (useSso)
            {
                _groupChat = new GroupChat(_userSipUri, ocsServer, lookupServerUri, chatRoomName);
            }
            else
            {
                // If we can't find the password in the registry, set to "default" and fail on startup.
                var value = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\hubot-msgc", false)?.GetValue("password");
                var ocsPassword = value?.ToString() ?? "default";
                if (ocsPassword.Equals("default"))
                {
                    _eventLog.WriteEntry(@"Failed to find password key in HKEY_LOCAL_MACHINE\SOFTWARE\hubot-msg, using value from App.config", EventLogEntryType.Warning);
                    ocsPassword = ConfigurationManager.AppSettings["OcsPassword"];
                }
                _groupChat = new GroupChat(_userSipUri, ocsServer, ocsUsername, ocsPassword, lookupServerUri,
                    chatRoomName);
            }

            _hubot = new Hubot(hubotUri);
        }

        // Just for testing...
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
                _eventLog.WriteEntry("Connecting to Hubot...");
                _hubot.ConnectAsync(_cancellationToken).Wait(_cancellationToken);
                _hubot.TextMessageReceived += HubotTextMessageReceived;
                Task.Run(() => _hubot.ListenAsync(_cancellationToken), _cancellationToken);
                _eventLog.WriteEntry("Connected to Hubot.");
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception while connecting to Hubot: " + exception.Message, EventLogEntryType.Error);
                throw;
            }

            try
            {
                _eventLog.WriteEntry("Connecting to GroupChat...");
                _groupChat.TextMessageReceived += GroupChatTextMessageReceivedAsync;
                _groupChat.Connect();
                _eventLog.WriteEntry("Connected to GroupChat.");
                _groupChat.Send("Connected to GroupChat and Hubot.");
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception connecting to GroupChat: " + exception.Message, EventLogEntryType.Error);
                throw;
            }
        }

        protected override void OnStop()
        {
            base.OnStop();
            _eventLog.WriteEntry("OnStop");

            try
            {
                _eventLog.WriteEntry("Disconnecting from GroupChat...");
                _groupChat.Disconnect();
                _eventLog.WriteEntry("Disconnected from GroupChat..");
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception disconnecting from GroupChat: " + exception.Message, EventLogEntryType.Error);
            }

            try
            {
                _eventLog.WriteEntry("Disconnecting from Hubot...");
                // TODO send Hubot room leave message
                _hubot.DisconnectAsync(_cancellationToken).Wait(_cancellationToken);
                _eventLog.WriteEntry("Disconnected from Hubot...");
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception disconnecting from Hubot: " + exception.Message, EventLogEntryType.Error);
            }
            _cancellationTokenSource.Cancel();
            _eventLog.WriteEntry("Stopped");
        }

        private void HubotTextMessageReceived(object sender, TextMessageReceivedEventArgs e)
        {
            try
            {
                _groupChat.Send(e.TextMessage.Text);
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Error sending to GroupChat: " + exception.Message, EventLogEntryType.Error);
            }
            
        }

        private async void GroupChatTextMessageReceivedAsync(object sender, TextMessageReceivedEventArgs e)
        {
            // Don't send messages from Hubot back to itself...
            var userSipUri = new Uri(e.TextMessage.UserName);
            if (!userSipUri.Equals(_userSipUri))
            {
                try
                {
                    await _hubot.SendAsync(e.TextMessage, _cancellationToken);
                }
                catch (Exception exception)
                {
                    _eventLog.WriteEntry("Error sending to Hubot: " + exception.Message, EventLogEntryType.Error);
                }
            }
        }
    }
}
