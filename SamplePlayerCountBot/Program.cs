using CommsLib;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SamplePlayerCountBot
{
    class Program
    {
        static void Main(string[] args) => new Program().RunBotAsync().GetAwaiter().GetResult();
        public DiscordSocketClient Client { get { return _client; } }
        public string StorageLocation { get { return SettingsFolder + @"\properties.txt"; } }
        public static Program Instance;
        public string token = "";
        private string TokenLocation { get { return SettingsFolder + @"\token.txt"; } }
        private string SettingsFolder { get { return @"C:\DiscordBotSettings"; } }
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;
        public const string prefix = "-";
        public bool downIntentionally = false;
        public bool serverUp = false;
        public BackgroundWorker worker;
        public TimerService EditTimer;
        #region SETTINGS_FILE
        public Dictionary<string, string> Settings = new Dictionary<string, string>();
        const string DefaultSettings = "MESSAGE_ID 0\nCHANNEL_ID 0\nINTERVAL_SEC 15\nSERVER_NAME Sample Server Name\nMMF_NAME discord-bot\nOWNER_ID 0";
        public string GetSetting(string key)
        {
            if (Settings.ContainsKey(key))
                return Settings[key];
            else
                return "null";
        }
        private void MakeSettings()
        {
            if(!Directory.Exists(SettingsFolder))
                Directory.CreateDirectory(SettingsFolder);
            FileStream stream = new FileStream(StorageLocation, FileMode.OpenOrCreate, FileAccess.Write);
            foreach (string Key in Settings.Keys)
            {
                byte[] Keybytes = Encoding.UTF8.GetBytes(Key);
                stream.Write(Keybytes, 0, Keybytes.Length);
                byte[] ValueBytes = Encoding.UTF8.GetBytes(' ' + Settings[Key] + '\n');
                stream.Write(ValueBytes, 0, ValueBytes.Length);
            }
            stream.Close();
        }
        private void LoadSettings(string s)
        {
            StringReader reader = new StringReader(s);
            Settings.Clear();
            while (true)
            {
                string p = reader.ReadLine();
                if (p == null) break;
                string[] data = p.Split(' ');
                if (data.Length > 1)
                    Settings.Add(data[0], data.ConcatStringArray(1, data.Length - 1));
                else
                    LogMessage(new LogMessage(LogSeverity.Warning, "SettingsLoader", "Error parsing line:\n" + p));
            }
            reader.Close();
        }
        public void SetSetting(string key, string value)
        {
            if (Settings.ContainsKey(key))
                Settings[key] = value;
            else
                Settings.Add(key, value);
            MakeSettings();
        }
        #endregion
        private async Task RunBotAsync()
        {
            #region SETTINGS
            if (!Directory.Exists(SettingsFolder))
                Directory.CreateDirectory(SettingsFolder);
            if (File.Exists(TokenLocation))
                token = File.ReadAllText(TokenLocation);
            else
            {
                File.Create(TokenLocation);
                await LogMessage(new LogMessage(LogSeverity.Warning, "SettingsLoader", "Token not in file."));
                return;
            }
            if (File.Exists(StorageLocation))
                LoadSettings(File.ReadAllText(StorageLocation));
            else
            {
                File.Create(StorageLocation);
                File.WriteAllText(StorageLocation, DefaultSettings);
                LoadSettings(DefaultSettings);
                await LogMessage(new LogMessage(LogSeverity.Warning, "SettingsLoader", "Settings not made, creating file now."));
                return;
            }
            #endregion
            #region Discord Bot Setup
            Instance = this;
            _client = new DiscordSocketClient();
            _commands = new CommandService();
            _services = new ServiceCollection().AddSingleton(_client).AddSingleton(_commands).BuildServiceProvider();
            _client.Log += LogMessage;
            await RegisterCommandsAsync();
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            ConsoleColor temp = Console.BackgroundColor;
            ConsoleColor temp2 = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Green;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Starting...");
            Console.WriteLine("Bot by BlazingFlame#0001, https://github.com/DanielWillett, ");
            Console.Title = "Sample Playercount Bot";
            Console.BackgroundColor = temp;
            Console.ForegroundColor = temp2;
            #endregion
            #region Timers and Workers
            worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;
            EditTimer = new TimerService(_client);
            if (EditTimer != null)
                EditTimer.Start();
            else
                await LogMessage(new Discord.LogMessage(LogSeverity.Critical, "EditTimer", "EditTimer is null"));

            if (Settings["MESSAGE_ID"] != "0" && Settings["CHANNEL_ID"] != "0")
                worker.RunWorkerAsync();
            else
                await LogMessage(new LogMessage(LogSeverity.Error, "BkgrWorker", "No message ID has been set, use \"-startplayercount\" to initialize it."));
            #endregion

            await Task.Delay(-1);
        }
        public Task LogMessage(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }
        public async Task RegisterCommandsAsync()
        {
            _client.Ready += ReadyAsync;
            _client.MessageReceived += HandleCommandAsync;
            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
        }
        public async Task ReadyAsync()
        {
            Instance = this;
            await Client.SetGameAsync("Unturned");
        }
        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (message == null) return;
            var context = new SocketCommandContext(_client, message);
            if (message.Author.IsBot) return;
            int argPos = 0;
            if (message.HasStringPrefix(prefix, ref argPos))
            {
                var result = await _commands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess)
                    if (result.Error.ToString() != "UnknownCommand")
                    {
                        ConsoleColor temp = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR\n\n" + result.Error + "\n" + result.ErrorReason + "\n\nERROR COMPLETE");
                        Console.ForegroundColor = temp;
                    }

            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen(Instance.GetSetting("MMF_NAME"), MMFInterface.Length);
                MemoryMappedViewAccessor ServerAccessor = mmf.CreateViewAccessor();
                MemoryMappedFile handler = MemoryMappedFile.CreateOrOpen(Instance.GetSetting("MMF_NAME") + "-handler", 1);
                MemoryMappedViewAccessor HandlerAccessor = handler.CreateViewAccessor();
                byte[] answer = new byte[MMFInterface.Length];
                ServerAccessor.ReadArray(0, answer, 0, answer.Length);
                if (answer[MMFInterface.Length - 1] == 1)
                {
                    while (true)
                    {
                        FPlayerList RecievedList;
                        try
                        {
                            RecievedList = MMFInterface.DecodeMessage(answer);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message == MMFInterface.NOT_HANDLED_MESSAGE)
                            {
                                ServerAccessor.WriteArray(0, new byte[MMFInterface.Length], 0, MMFInterface.Length);
                                HandlerAccessor.Write(0, 2);
                                ServerAccessor.Dispose();
                                HandlerAccessor.Dispose();
                                mmf.Dispose();
                                handler.Dispose();
                                break;
                            }
                            else
                            {
                                ServerAccessor.Dispose();
                                HandlerAccessor.Dispose();
                                mmf.Dispose();
                                handler.Dispose();
                                Instance.LogMessage(new LogMessage(LogSeverity.Error, "Worker", "Error handling response.", ex));
                                throw ex;
                            }
                        }
                        EditTimer.LastReceivedPlayerlist = RecievedList;
                        HandlerAccessor.Write(0, 1);
                        ServerAccessor.Dispose();
                        HandlerAccessor.Dispose();
                        mmf.Dispose();
                        handler.Dispose();
                        break;
                    }
                }
                ServerAccessor.Dispose();
                HandlerAccessor.Dispose();
                mmf.Dispose();
                handler.Dispose();
            }
        }
        // TimerService by Joe4evr
        // https://gist.github.com/Joe4evr/967949a477ed0c6c841407f0f25fa730
        public class TimerService
        {
            public FPlayerList LastReceivedPlayerlist;
            public FPlayerList LastSentPlayerList;
            private readonly Timer _timer;

            public TimerService(DiscordSocketClient client)
            {
                _timer = new Timer(async _ =>
                {
                    bool run = true;
                    if (LastReceivedPlayerlist.Equals(LastSentPlayerList) && LastReceivedPlayerlist.timestamp != new DateTime() && DateTime.Now - LastReceivedPlayerlist.timestamp <= TimeSpan.FromMinutes(2))
                        run = false;
                    if (Program.Instance.Settings["CHANNEL_ID"] == "0" || Program.Instance.Settings["MESSAGE_ID"] == "0")
                        run = false;
                    ulong ChannelID = 0;
                    if (!ulong.TryParse(Program.Instance.Settings["CHANNEL_ID"], out ChannelID))
                        run = false;
                    ulong MessageID = 0;
                    if (!ulong.TryParse(Program.Instance.Settings["MESSAGE_ID"], out MessageID))
                        run = false;
                    try
                    {
                        if (run && client.GetChannel(ChannelID) != null)
                        {
                            SocketTextChannel channel = (SocketTextChannel)client.GetChannel(ChannelID);
                            try
                            {
                                await channel.GetMessageAsync(MessageID);
                            }
                            catch (Discord.Net.HttpException)
                            {
                                await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "HTML Error when finding message."));
                                return;
                            }
                            catch (System.Net.Http.HttpRequestException)
                            {
                                await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "HTML Error when finding message."));
                                return;
                            }
                            catch (TaskCanceledException)
                            {
                                await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "Task was cancelled when finding message."));
                                return;
                            }
                        }
                    }
                    catch (Discord.Net.HttpException)
                    {
                        await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "HTML Error when finding message."));
                        return;
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "HTML Error when finding message."));
                        return;
                    }
                    catch (TaskCanceledException)
                    {
                        await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "Task was cancelled when finding message."));
                        return;
                    }
                    try
                    {
                        if (run && client.GetChannel(ChannelID) != null)
                        {
                            SocketTextChannel channel = (SocketTextChannel)client.GetChannel(ChannelID);
                            if (run && await channel.GetMessageAsync(MessageID) != null)
                            {
                                RestUserMessage message = (RestUserMessage)await channel.GetMessageAsync(MessageID);
                                if (LastReceivedPlayerlist._players == null || (DateTime.Now - LastReceivedPlayerlist.timestamp > TimeSpan.FromMinutes(2) && !Program.Instance.downIntentionally)
                                || ((LastReceivedPlayerlist.players.Length == 1 || LastReceivedPlayerlist.players.Length == 2)
                                && LastReceivedPlayerlist.players[0] == "OFFLINE"))
                                {
                                    if (LastReceivedPlayerlist._players == null || (LastReceivedPlayerlist.PlayerCount == 2 && LastReceivedPlayerlist.players[0] == "OFFLINE" && LastReceivedPlayerlist.players[1] == "INTENTIONAL"))
                                    {
                                        await message.ModifyAsync(msg => msg.Embed = EmbedMaker.PlayerListOffline(Discord.Color.Red, client, true, Program.Instance.GetSetting("SERVER_NAME")));
                                        Program.Instance.downIntentionally = true;
                                    }
                                    else
                                    {
                                        await message.ModifyAsync(msg => msg.Embed = EmbedMaker.PlayerListOffline(Discord.Color.Red, client, false, Program.Instance.GetSetting("SERVER_NAME")));
                                        Program.Instance.downIntentionally = false;
                                    }
                                    if (Program.Instance.serverUp)
                                        Program.Instance.serverUp = false;
                                    if (!Program.Instance.serverUp)
                                        await client.SetStatusAsync(UserStatus.Invisible);
                                    LastSentPlayerList = LastReceivedPlayerlist;
                                }
                                else
                                {
                                    await message.ModifyAsync(msg => msg.Embed = EmbedMaker.PlayerList(LastReceivedPlayerlist, Discord.Color.DarkGreen, client, Program.Instance.GetSetting("SERVER_NAME")));
                                    await client.SetStatusAsync(UserStatus.Online);
                                    LastSentPlayerList = LastReceivedPlayerlist;
                                    if (LastReceivedPlayerlist.PlayerCount == 0)
                                        await client.SetGameAsync("with no players online.", type: ActivityType.Playing);
                                    else
                                        await client.SetGameAsync("with " + LastReceivedPlayerlist.PlayerCount + $" player{LastReceivedPlayerlist.PlayerCount.s()} online.", type: ActivityType.Playing);
                                    if (!Program.Instance.serverUp)
                                    {
                                        Program.Instance.serverUp = true;
                                        Program.Instance.downIntentionally = false;
                                    }
                                }
                            }
                        }
                    }
                    catch (Discord.Net.HttpException)
                    {
                        await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "HTML Error when finding message."));
                        return;
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "HTML Error when finding message."));
                        return;
                    }
                    catch (TaskCanceledException)
                    {
                        await Program.Instance.LogMessage(new LogMessage(LogSeverity.Error, "TimerService", "Task was cancelled when finding message."));
                        return;
                    }

                }, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(double.Parse(Program.Instance.GetSetting("INTERVAL_SEC"))));
            }
            public void Stop()
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            public void Start()
            {
                _timer.Change(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(double.Parse(Program.Instance.GetSetting("INTERVAL_SEC"))));
            }
        }
    }
    public static class EXT
    {
        public static string ConcatStringArray(this string[] array, int StartIndex, int EndIndex, char deliminator = ' ')
        {
            string rtn = string.Empty;
            for (int i = StartIndex; i <= EndIndex; i++)
            {
                rtn += array[i];
                if (i != EndIndex)
                    rtn += deliminator;
            }
            return rtn;
        }
        public static string s(this int n)
        {
            if (n == 1)
                return "";
            else
                return "s";
        }
    }
}
