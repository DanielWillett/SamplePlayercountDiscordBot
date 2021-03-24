using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SamplePlayerCountBot
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("startplayercount")]
        public async Task StartPlayerCount()
        {
            if (Context.Message.Author.Id != ulong.Parse(Program.Instance.GetSetting("OWNER_ID")))
                return;
            EmbedBuilder e = new EmbedBuilder { Timestamp = DateTime.Now };
            RestUserMessage message = await Context.Channel.SendMessageAsync(embed: e.Build());
            Program.Instance.SetSetting("MESSAGE_ID", message.Id.ToString());
            Program.Instance.SetSetting("CHANNEL_ID", message.Channel.Id.ToString());
            if (!Program.Instance.worker.IsBusy)
            {
                Program.Instance.worker.RunWorkerAsync();
            }
            Program.Instance.EditTimer.Start();
        }
        [Command("setmessage")]
        public async Task SetMessage(string messageid, string channelid)
        {
            if (Context.Message.Author.Id == ulong.Parse(Program.Instance.GetSetting("OWNER_ID")))
            {
                if (ulong.TryParse(messageid, out ulong msg) && ulong.TryParse(channelid, out ulong chl))
                {
                    Program.Instance.SetSetting("MESSAGE_ID", msg.ToString());
                    Program.Instance.SetSetting("CHANNEL_ID", chl.ToString());
                    await Context.Channel.SendMessageAsync($"Set message ID to **{msg}** and channel ID to **{chl}**");
                }
                else
                {
                    await Context.Channel.SendMessageAsync($"Failed to parse to **UInt64**.");
                }
            }
            else
            {
                await Context.Channel.SendMessageAsync($"Only <@{ Program.Instance.GetSetting("OWNER_ID") }> can do this.");
            }
        }
        [Command("intentional")]
        public async Task IntentionalShutdown()
        {
            if (Context.Message.Author.Id == ulong.Parse(Program.Instance.GetSetting("OWNER_ID")))
            {
                if (!Program.Instance.serverUp)
                {
                    Program.Instance.downIntentionally = true;
                    if (Program.Instance.Client.GetChannel(ulong.Parse(Program.Instance.GetSetting("CHANNEL_ID"))) != null)
                    {
                        SocketTextChannel channel = (SocketTextChannel)Program.Instance.Client.GetChannel(ulong.Parse(Program.Instance.GetSetting("CHANNEL_ID")));
                        if (await channel.GetMessageAsync(ulong.Parse(Program.Instance.GetSetting("MESSAGE_ID"))) != null)
                        {
                            RestUserMessage message = (RestUserMessage)await channel.GetMessageAsync(ulong.Parse(Program.Instance.GetSetting("MESSAGE_ID")));
                            await message.ModifyAsync(msg => msg.Embed = EmbedMaker.PlayerListOffline(Color.Red, Program.Instance.Client, true, Program.Instance.GetSetting("SERVER_NAME")));
                            await Context.Message.Channel.SendMessageAsync("Changed down-message to intentional.");
                        }
                    }
                }
                else
                {
                    await Context.Channel.SendMessageAsync($"The server is still up, if this isn't right, try again in **{Program.Instance.GetSetting("INTERVAL_SEC")} seconds**.");
                }
            }
            else
            {
                await Context.Channel.SendMessageAsync($"Only <@{Program.Instance.GetSetting("OWNER_ID")}> can do this.");
            }
        }
    }
}
