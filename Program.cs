using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace TwitchBot;

class Program
{
    static async Task Main(string[] args)
    {
        var bot = new TwilightBot();
        await bot.RunAsync();
    }
}

public class Config
{
    public string Username { get; set; } = "twilightbot";
    public string Token { get; set; } = "oauth:your_token_here";
    public string Channel { get; set; } = "#thatonemutt";
    public string Prefix { get; set; } = "!";
    public bool AutoModEnabled { get; set; } = true;
    public int CapsLimit { get; set; } = 70;
    public int LinkTimeout { get; set; } = 60;
    public int SpamTimeout { get; set; } = 300;
    public int EmoteLimit { get; set; } = 15;
    public List<string> BannedWords { get; set; } = new();
    public List<string> AllowedLinks { get; set; } = new() { "youtube.com", "discord.gg", "twitch.tv" };
    public List<TimedMessage> TimedMessages { get; set; } = new();

    public void Load()
    {
        if (File.Exists("config.json"))
        {
            var json = File.ReadAllText("config.json");
            var loaded = JsonSerializer.Deserialize<Config>(json);
            if (loaded != null)
            {
                Username = loaded.Username;
                Token = loaded.Token;
                Channel = loaded.Channel;
                Prefix = loaded.Prefix;
                AutoModEnabled = loaded.AutoModEnabled;
                CapsLimit = loaded.CapsLimit;
                LinkTimeout = loaded.LinkTimeout;
                SpamTimeout = loaded.SpamTimeout;
                EmoteLimit = loaded.EmoteLimit;
                BannedWords = loaded.BannedWords;
                AllowedLinks = loaded.AllowedLinks;
                TimedMessages = loaded.TimedMessages;
            }
        }
        else Save();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("config.json", json);
    }
}

public class TimedMessage
{
    public string Message { get; set; } = "";
    public int Interval { get; set; } = 10;
    public bool Enabled { get; set; } = true;
    public long LastSent { get; set; } = 0;
}

public class Command
{
    public string Name { get; set; } = "";
    public string Response { get; set; } = "";
    public int Cooldown { get; set; } = 5;
    public string Permission { get; set; } = "everyone";
    public long LastUsed { get; set; } = 0;
}

public class CommandsData
{
    public List<Command> Commands { get; set; } = new();
}

public class TwilightBot
{
    private Config _config = new();
    private CommandsData _commands = new();
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _connected = false;
    private bool _running = true;
    private int _viewers = 0;
    private int _messages = 0;
    private DateTime _startTime = DateTime.Now;
    private List<string> _chatHistory = new();
    private string _currentTab = "dashboard";
    private readonly object _lock = new();

    public async Task RunAsync()
    {
        _config.Load();
        LoadCommands();

        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Twilight Bot").Centered().Color(Color.Gold3_1));
        AnsiConsole.MarkupLine("[dim]Bearer of the Master Sword[/]\n");

        while (_running)
        {
            ShowTabMenu();
            switch (_currentTab)
            {
                case "dashboard": await ShowDashboard(); break;
                case "control": ShowControl(); break;
                case "commands": ShowCommands(); break;
                case "settings": ShowSettings(); break;
            }
            await Task.Delay(100);
        }
    }

    private void ShowTabMenu()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Twilight Bot").Centered().Color(Color.Gold3_1));

        var status = _connected ? "[green]Connected[/]" : "[red]Disconnected[/]";
        var uptime = DateTime.Now - _startTime;

        AnsiConsole.Write(new Grid().AddColumn().AddRow(new Panel($"Status: {status} | Viewers: {_viewers} | Messages: {_messages} | Uptime: {uptime:hh\\:mm\\:ss}").Border(BoxBorder.Rounded)));
        AnsiConsole.WriteLine();

        var tabs = new[] { ("[1] Dashboard", _currentTab == "dashboard"), ("[2] Control", _currentTab == "control"),
                         ("[3] Commands", _currentTab == "commands"), ("[4] Settings", _currentTab == "settings") };
        AnsiConsole.MarkupLine(string.Join(" | ", tabs.Select(t => t.Item2 ? $"[gold1]{t.Item1}[/]" : $"[dim]{t.Item1}[/]")));
        AnsiConsole.WriteLine();
    }

    private async Task ShowDashboard()
    {
        var chatText = _chatHistory.Count > 0 ? string.Join("\n", _chatHistory.TakeLast(20)) : "Welcome!\nConnect to start chatting.";

        var leftPanel = new Panel(chatText).Header("[cyan]Hyrule Chat[/]").Border(BoxBorder.Rounded).Expand();

        var statsTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Magenta1);
        statsTable.AddColumn("Stat");
        statsTable.AddColumn("Value");
        statsTable.AddRow("Uptime", (DateTime.Now - _startTime).ToString("hh\\:mm\\:ss"));
        statsTable.AddRow("Viewers", _viewers.ToString());
        statsTable.AddRow("Messages", _messages.ToString());

        var rightTop = new Panel(statsTable).Header("[magenta]Impa's Wisdom[/]").Border(BoxBorder.Rounded);

        var quickTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Green1);
        quickTable.AddColumn("Command");
        quickTable.AddColumn("Description");
        quickTable.AddRow("!discord", "Discord link");
        quickTable.AddRow("!socials", "Social media");

        var rightBottom = new Panel(quickTable).Header("[green]Wolf Link Items[/]").Border(BoxBorder.Rounded);

        AnsiConsole.Write(new Rows(leftPanel, rightTop, rightBottom));
        AnsiConsole.MarkupLine("\n[dim]C=Connect D=Disconnect 1-4=Tabs Q=Quit[/]");

        if (Console.KeyAvailable) await HandleKey(Console.ReadKey(true));
    }

    private void ShowControl()
    {
        var connTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Blue1);
        connTable.AddColumn("Action");
        connTable.AddRow("[green]C[/] Connect");
        connTable.AddRow("[red]D[/] Disconnect");

        var connPanel = new Panel(connTable).Header("[blue]Light Spirit[/]").Border(BoxBorder.Rounded);

        var modTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Red1);
        modTable.AddColumn("Protection");
        modTable.AddColumn("Status");
        modTable.AddRow("Caps", _config.AutoModEnabled ? "[green]ON[/]" : "[red]OFF[/]");
        modTable.AddRow("Links", _config.AutoModEnabled ? "[green]ON[/]" : "[red]OFF[/]");

        var modPanel = new Panel(modTable).Header("[red]Twilight Protection[/]").Border(BoxBorder.Rounded);

        AnsiConsole.Write(connPanel);
        AnsiConsole.Write(modPanel);
        AnsiConsole.MarkupLine("\n[dim]C/D=Connection T=Toggle Q=Quit[/]");

        if (Console.KeyAvailable) HandleKey(Console.ReadKey(true)).Wait();
    }

    private void ShowCommands()
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Cyan1);
        table.AddColumn("Command").Centered();
        table.AddColumn("Response");
        table.AddColumn("CD").Centered();

        foreach (var cmd in _commands.Commands)
            table.AddRow($"[cyan]{cmd.Name}[/]", cmd.Response.Length > 30 ? cmd.Response[..30] + "..." : cmd.Response, $"[yellow]{cmd.Cooldown}s[/]");

        AnsiConsole.Write(new Panel(table).Header("[cyan]Gear Bag[/]").Border(BoxBorder.Rounded));
        AnsiConsole.MarkupLine("\n[dim]A=Add E=Edit D=Delete Q=Quit[/]");

        if (Console.KeyAvailable) HandleKey(Console.ReadKey(true)).Wait();
    }

    private void ShowSettings()
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Gold3_1);
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Username", _config.Username);
        table.AddRow("Channel", _config.Channel);
        table.AddRow("Prefix", _config.Prefix);

        AnsiConsole.Write(new Panel(table).Header("[gold1]Hero's Credentials[/]").Border(BoxBorder.Rounded));
        AnsiConsole.MarkupLine("\n[dim]E=Edit Q=Quit[/]");

        if (Console.KeyAvailable) HandleKey(Console.ReadKey(true)).Wait();
    }

    private async Task HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q: _running = false; await DisconnectAsync(); break;
            case ConsoleKey.D1: _currentTab = "dashboard"; break;
            case ConsoleKey.D2: _currentTab = "control"; break;
            case ConsoleKey.D3: _currentTab = "commands"; break;
            case ConsoleKey.D4: _currentTab = "settings"; break;
            case ConsoleKey.C: await ConnectAsync(); break;
            case ConsoleKey.D: await DisconnectAsync(); break;
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync("irc.chat.twitch.tv", 6667);
            _reader = new StreamReader(_client.GetStream(), Encoding.UTF8);
            _writer = new StreamWriter(_client.GetStream(), Encoding.UTF8) { AutoFlush = true };
            await _writer.WriteLineAsync($"PASS {_config.Token}");
            await _writer.WriteLineAsync($"NICK {_config.Username}");
            await _writer.WriteLineAsync($"JOIN {_config.Channel}");
            _connected = true;
            AddChat("Connected!");
            _ = Task.Run(ReceiveLoopAsync);
            _ = Task.Run(TimedMessageLoopAsync);
        }
        catch (Exception ex) { AddChat($"Error: {ex.Message}"); }
    }

    private async Task DisconnectAsync()
    {
        _connected = false;
        _client?.Close();
        AddChat("Disconnected");
    }

    private async Task ReceiveLoopAsync()
    {
        while (_connected && _reader != null)
        {
            try
            {
                var line = await _reader.ReadLineAsync();
                if (line == null) break;
                if (line.StartsWith("PING")) await _writer!.WriteLineAsync("PONG :tmi.twitch.tv");
                else if (line.Contains("PRIVMSG")) await HandleIrcMessageAsync(line);
            }
            catch { break; }
        }
        _connected = false;
    }

    private async Task HandleIrcMessageAsync(string line)
    {
        var privmsgIdx = line.IndexOf("PRIVMSG");
        if (privmsgIdx < 0) return;
        var parts = line[(privmsgIdx + 8)..].Split(new[] { " :" }, 2, StringSplitOptions.None);
        if (parts.Length != 2) return;
        var channel = parts[0].Trim();
        var message = parts[1];
        var userStart = line.IndexOf('!');
        var username = userStart > 1 ? line[1..userStart] : "unknown";
        AddChat($"[{channel}] {username}: {message}");
        _messages++;
        if (message.StartsWith(_config.Prefix)) await HandleCommandAsync(username, message);
    }

    private async Task HandleCommandAsync(string username, string message)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var cmd = _commands.Commands.FirstOrDefault(c => c.Name == parts[0]);
        if (cmd == null) return;
        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (now - cmd.LastUsed < cmd.Cooldown) return;
        await SendMessageAsync(cmd.Response.Replace("{user}", username));
        cmd.LastUsed = now;
        SaveCommands();
    }

    private async Task SendMessageAsync(string message)
    {
        if (_connected && _writer != null)
        {
            await _writer.WriteLineAsync($"PRIVMSG {_config.Channel} :{message}");
            AddChat($"[Bot] {message}");
        }
    }

    private async Task TimedMessageLoopAsync()
    {
        while (_running)
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            if (!_connected) continue;
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            foreach (var tm in _config.TimedMessages.Where(t => t.Enabled))
                if (now - tm.LastSent >= tm.Interval * 60) { await SendMessageAsync(tm.Message); tm.LastSent = now; _config.Save(); }
        }
    }

    private void AddChat(string message)
    {
        lock (_lock) { _chatHistory.Add($"{DateTime.Now:HH:mm:ss} {message}"); if (_chatHistory.Count > 100) _chatHistory.RemoveAt(0); }
    }

    private void LoadCommands()
    {
        if (File.Exists("commands.json"))
            _commands = JsonSerializer.Deserialize<CommandsData>(File.ReadAllText("commands.json")) ?? new CommandsData();
        else
        {
            _commands.Commands = new List<Command>
            {
                new() { Name = "!discord", Response = "Join: discord.gg/example", Cooldown = 5 },
                new() { Name = "!socials", Response = "Twitter @example", Cooldown = 5 }
            };
            SaveCommands();
        }
    }

    private void SaveCommands() => File.WriteAllText("commands.json", JsonSerializer.Serialize(_commands, new JsonSerializerOptions { WriteIndented = true }));
}
