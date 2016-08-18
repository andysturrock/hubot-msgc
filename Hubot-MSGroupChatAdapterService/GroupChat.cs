using System;
using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.GroupChat;
using Microsoft.Rtc.Signaling;

namespace Hubot_MSGroupChatAdapterService
{
    class GroupChat
    {
        public static UserEndpoint ConnectOfficeCommunicationServer(Uri userSipUri, string ocsServer, string username, string password)
        {
            // Create the OCS UserEndpoint and attempt to connect to OCS
            Console.WriteLine("Connecting to OCS... [{0}]", ocsServer);

            // Use the appropriate SipTransportType depending on current OCS deployment
            ClientPlatformSettings platformSettings = new ClientPlatformSettings("GroupChat.Test", SipTransportType.Tls);
            CollaborationPlatform collabPlatform = new CollaborationPlatform(platformSettings);

            // Initialize the platform
            collabPlatform.EndStartup(collabPlatform.BeginStartup(null, null));

            // You can also pass in the server's port # here.
            UserEndpointSettings userEndpointSettings = new UserEndpointSettings(userSipUri.ToString(), ocsServer);

            // When usingSso is true use the current users credentials, otherwise use username and password
            userEndpointSettings.Credential = new NetworkCredential(username, password);

            UserEndpoint userEndpoint = new UserEndpoint(collabPlatform, userEndpointSettings);

            // Login to OCS.
            userEndpoint.EndEstablish(userEndpoint.BeginEstablish(null, null));

            Console.WriteLine("\tSuccess");
            return userEndpoint;
        }

        public static GroupChatEndpoint ConnectGroupChatServer(UserEndpoint userEndpoint, Uri lookupServerUri)
        {
            Console.WriteLine("Connecting to Group Chat Server...");

            GroupChatEndpoint groupChatEndpoint = new GroupChatEndpoint(lookupServerUri, userEndpoint);

            groupChatEndpoint.EndEstablish(groupChatEndpoint.BeginEstablish(null, null));

            Console.WriteLine("\tSuccess");
            return groupChatEndpoint;
        }

        public static ChatRoomSnapshot RoomSearchExisting(GroupChatEndpoint groupChatEndpoint, string chatRoomName)
        {
            Console.WriteLine(String.Format("Searching for chat room [{0}]...", chatRoomName));

            GroupChatServices chatServices = groupChatEndpoint.GroupChatServices;

            ReadOnlyCollection<ChatRoomSnapshot> chatRooms = chatServices.EndBrowseChatRoomsByCriteria(
                chatServices.BeginBrowseChatRoomsByCriteria(chatRoomName, false, null, null));

            Console.WriteLine(String.Format("\tFound {0} chat room(s):", chatRooms.Count));
            if (chatRooms.Count > 0)
            {
                foreach (ChatRoomSnapshot snapshot in chatRooms)
                    Console.WriteLine(String.Format("\tname: {0}\n\turi:{1}", snapshot.Name, snapshot.ChatRoomUri));
                return chatRooms[0];
            }
            return null;
        }

        public static ChatRoomSession RoomJoinExisting(GroupChatEndpoint groupChatEndpoint, ChatRoomSummary summary)
        {
            Console.WriteLine(String.Format("Joining chat room by NAME [{0}]...", summary.Name));

            ChatRoomSession session = new ChatRoomSession(groupChatEndpoint);
            session.EndJoin(session.BeginJoin(summary, null, null));

            Console.WriteLine("\tSuccess");

            return session;
        }

        public static void DisconnectGroupChatServer(GroupChatEndpoint groupChatEndpoint)
        {
            Console.WriteLine("Disconnecting from Group Chat Server...");

            groupChatEndpoint.EndTerminate(groupChatEndpoint.BeginTerminate(null, null));

            Console.WriteLine("\tSuccess");
        }

        public static void DisconnectOfficeCommunicationServer(UserEndpoint userEndpoint)
        {
            Console.WriteLine("Disconnecting from OCS...");

            userEndpoint.EndTerminate(userEndpoint.BeginTerminate(null, null));

            CollaborationPlatform platform = userEndpoint.Platform;
            platform.EndShutdown(platform.BeginShutdown(null, null));

            Console.WriteLine("\tSuccess");
        }
    }
}
