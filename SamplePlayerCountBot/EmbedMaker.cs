using CommsLib;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SamplePlayerCountBot
{
    public static class EmbedMaker
    {
        public static Embed PlayerListOffline(Color Color, DiscordSocketClient Client, bool intentional, string Title)
        {
            string description = string.Empty;
            if (intentional)
                description = "This shutdown was intentional and the server will hopefully be up as soon as possible. Thank you for your patience.";
            else
                description = $"The server hasn't responded in 2 minutes.\nThis is likely caused by a crash or accidental shutdown, please contact <@{Program.Instance.GetSetting("OWNER_ID")}> through Discord.";
            EmbedBuilder e = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = Title,
                    IconUrl = Client.CurrentUser.GetAvatarUrl()
                },
                Color = Color,
                Title = $"Server Offline",
                Description = description,
                Timestamp = DateTime.Now
            };
            return e.Build();
        }

        public static Embed PlayerList(FPlayerList players, Color Color, DiscordSocketClient Client, string Title)
        {
            string temp = String.Empty;
            if (players.PlayerCount == 0)
            {
                temp += "No players online.";
            }
            else
            {
                temp += "Players:";
            }
            foreach (string player in players.players)
            {
                temp += "\n" + player;
            }
            EmbedBuilder e = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = Title,
                    IconUrl = Client.CurrentUser.GetAvatarUrl()
                },
                Color = Color,
                Title = $"Online Players ({players.PlayerCount}/24)",
                Description = temp,
                Timestamp = players.timestamp
            };
            return e.Build();
        }
    }
}
