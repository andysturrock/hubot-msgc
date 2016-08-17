using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubot_MSGroupChatAdapterService
{
    class TextMessage
    {
        public TextMessage(string type, long id, string userName, string room, string text)
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
        public long Id { get; set; }
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
