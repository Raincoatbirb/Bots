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
        else
        {
            Save();
        }
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
                case "dashboard":
                    await ShowDashboard();
                    break;
                case "control":
                    ShowControl();
                    break;
                case "commands":
                    ShowCommands();
                    break;
                case "settings":
                    ShowSettings();
                    break;
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

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Panel($"Status: {status} | Viewers: {_viewers} | Messages: {_messages} | Uptime: {uptime:hh\\:mm\\:ss}")
            .Border(BoxBorder.Rounded));

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        var tabs = new[] { ("[1] Dashboard", _currentTab == "dashboard"), ("[2] Control", _currentTab == "control"),
                         ("[3] Commands", _currentTab == "commands"), ("[4] Settings", _currentTab == "settings") };

        var tabRow = string.Join(" | ", tabs.Select(t => t.Item2 ? $"[gold1]{t.Item1}[/]" : $"[dim]{t.Item1}[/]"));
        AnsiConsole.MarkupLine(tabRow);
        AnsiConsole.WriteLine();
    }

    private async Task ShowDashboard()
    {
        var chatText = _chatHistory.Count > 0
            ? string.Join("\n", _chatHistory.TakeLast(20))
            : "Welcome to Twilight Bot!\nConnect to start chatting.";

        var leftPanel = new Panel(chatText)
            .Header("[cyan]Hyrule Chat[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);

        var statsTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Magenta1);
        statsTable.AddColumn("Stat");
        statsTable.AddColumn("Value");
        statsTable.AddRow("Stream Uptime", (DateTime.Now - _startTime).ToString("hh\\:mm\\:ss"));
        statsTable.AddRow("Viewers", _viewers.ToString());
        statsTable.AddRow("Messages", _messages.ToString());
        statsTable.AddRow("Status", _connected ? "[green]Connected[/]" : "[red]Disconnected[/]");

        var rightTop = new Panel(statsTable)
            .Header("[magenta]Impa's Wisdom[/]")
            .Border(BoxBorder.Rounded);

        var quickTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Green1);
        quickTable.AddColumn("Command");
        quickTable.AddColumn("Description");
        quickTable.AddRow("[cyan]!discord[/]", "Discord link");
        quickTable.AddRow("[cyan]!socials[/]", "Social media");
        quickTable.AddRow("[cyan]!uptime[/]", "Stream time");

        var rightBottom = new Panel(quickTable)
            .Header("[green]Wolf Link Items[/]")
            .Border(BoxBorder.Rounded);

        var rightPanel = new Layout("right")
            .SplitColumn(
                new Layout("top").Update(rightTop).Size(12),
                new Layout("bottom").Update(rightBottom)
            );

        var mainLayout = new Layout("root")
            .SplitRow(
                new Layout("left").Update(leftPanel),
                rightPanel
            );

        AnsiConsole.Write(mainLayout);

        AnsiConsole.MarkupLine("\n[dim]Press C to Connect | D to Disconnect | 1-4 to switch tabs | Q to Quit[/]");

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            await HandleKey(key);
        }
    }

    private void ShowControl()
    {
        var connTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Blue1);
        connTable.AddColumn("Action");
        connTable.AddRow("[green]C[/] Connect to Twitch");
        connTable.AddRow("[red]D[/] Disconnect");

        var connPanel = new Panel(connTable)
            .Header("[blue]Light Spirit Connection[/]")
            .Border(BoxBorder.Rounded);

        var modTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Red1);
        modTable.AddColumn("Protection");
        modTable.AddColumn("Status");
        modTable.AddRow("Caps Lock", _config.AutoModEnabled ? "[green]ON[/]" : "[red]OFF[/]");
        modTable.AddRow("Links", _config.AutoModEnabled ? "[green]ON[/]" : "[red]OFF[/]");
        modTable.AddRow("Spam", _config.AutoModEnabled ? "[green]ON[/]" : "[red]OFF[/]");

        var modPanel = new Panel(modTable)
            .Header("[red]Twilight Protection[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(connPanel);
        AnsiConsole.Write(modPanel);

        AnsiConsole.MarkupLine("\n[dim]Press C/D for connection, T to toggle AutoMod, Q to Quit[/]");

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            HandleKey(key).Wait();
        }
    }

    private void ShowCommands()
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Cyan1);
        table.AddColumn("Command").Centered();
        table.AddColumn("Response");
        table.AddColumn("Cooldown").Centered();

        foreach (var cmd in _commands.Commands)
        {
            table.AddRow($"[cyan]{cmd.Name}[/]", cmd.Response.Length > 40 ? cmd.Response[..40] + "..." : cmd.Response,
                        $"[yellow]{cmd.Cooldown}s[/]");
        }

        AnsiConsole.Write(new Panel(table).Header("[cyan]Gear Bag[/]").Border(BoxBorder.Rounded));

        AnsiConsole.MarkupLine("\n[dim]Press A to Add command | E to Edit | D to Delete | Q to Quit[/]");

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            HandleKey(key).Wait();
        }
    }

    private void ShowSettings()
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Gold3_1);
        table.AddColumn("Setting");
        table.AddColumn("Value");

        table.AddRow("[yellow]Username[/]", _config.Username);
        table.AddRow("[yellow]Channel[/]", _config.Channel);
        table.AddRow("[yellow]Prefix[/]", _config.Prefix);
        table.AddRow("[yellow]Caps Limit[/]", _config.CapsLimit.ToString());
        table.AddRow("[yellow]Link Timeout[/]", _config.LinkTimeout.ToString());
        table.AddRow("[yellow]Spam Timeout[/]", _config.SpamTimeout.ToString());

        AnsiConsole.Write(new Panel(table).Header("[gold1]Hero's Credentials[/]").Border(BoxBorder.Rounded));

        AnsiConsole.MarkupLine("\n[dim]Press E to Edit | Q to Quit[/]");

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            HandleKey(key).Wait();
        }
    }

    private async Task HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q:
                _running = false;
                await DisconnectAsync();
                break;
            case ConsoleKey.D1:
                _currentTab = "dashboard";
                break;
            case ConsoleKey.D2:
                _currentTab = "control";
                break;
            case ConsoleKey.D3:
                _currentTab = "commands";
                break;
            case ConsoleKey.D4:
                _currentTab = "settings";
                break;
            case ConsoleKey.C:
                await ConnectAsync();
                break;
            case ConsoleKey.D:
                await DisconnectAsync();
                break;
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
            await _writer.WriteLineAsync("CAP REQ :twitch.tv/commands twitch.tv/tags");

            _connected = true;
            AddChat("Connected to Twitch!");

            _ = Task.Run(ReceiveLoopAsync);
            _ = Task.Run(TimedMessageLoopAsync);
        }
        catch (Exception ex)
        {
            AddChat($"Connection error: {ex.Message}");
        }
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

                await HandleIrcMessageAsync(line);
            }
            catch { break; }
        }
        _connected = false;
    }

    private async Task HandleIrcMessageAsync(string line)
    {
        if (line.StartsWith("PING"))
        {
            await _writer!.WriteLineAsync("PONG :tmi.twitch.tv");
            return;
        }

        if (line.Contains("PRIVMSG"))
        {
            var parts = line.Split("PRIVMSG");
            if (parts.Length == 2)
            {
                var msgParts = parts[1].Split(new[] { " :" }, 2, StringSplitOptions.None);
                if (msgParts.Length == 2)
                {
                    var channel = msgParts[0].Trim();
                    var message = msgParts[1];

                    var userStart = line.IndexOf('!');
                    var username = userStart > 1 ? line[1..userStart] : "unknown";

                    AddChat($"[{channel}] {username}: {message}");
                    _messages++;

                    if (message.StartsWith(_config.Prefix))
                    {
                        await HandleCommandAsync(username, message);
                    }
                }
            }
        }
    }

    private async Task HandleCommandAsync(string username, string message)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var cmdName = parts[0];
        var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        var cmd = _commands.Commands.FirstOrDefault(c => c.Name == cmdName);
        if (cmd == null) return;

        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (now - cmd.LastUsed < cmd.Cooldown) return;

        var response = ProcessResponse(cmd.Response, username, args);
        await SendMessageAsync(response);

        cmd.LastUsed = now;
        SaveCommands();
    }

    private string ProcessResponse(string response, string username, string[] args)
    {
        response = response.Replace("{user}", username);
        if (args.Length > 0) response = response.Replace("{target}", args[0]);
        if (response.Contains("{uptime}"))
        {
            var uptime = DateTime.Now - _startTime;
            response = response.Replace("{uptime}", uptime.ToString("hh\\:mm\\:ss"));
        }
        return response;
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
            {
                if (now - tm.LastSent >= tm.Interval * 60)
                {
                    await SendMessageAsync(tm.Message);
                    tm.LastSent = now;
                    _config.Save();
                }
            }
        }
    }

    private void AddChat(string message)
    {
        lock (_lock)
        {
            _chatHistory.Add($"{DateTime.Now:HH:mm:ss} {message}");
            if (_chatHistory.Count > 100) _chatHistory.RemoveAt(0);
        }
    }

    private void LoadCommands()
    {
        if (File.Exists("commands.json"))
        {
            var json = File.ReadAllText("commands.json");
            _commands = JsonSerializer.Deserialize<CommandsData>(json) ?? new CommandsData();
        }
        else
        {
            _commands.Commands = new List<Command>
            {
                new() { Name = "!discord", Response = "Join our Discord: discord.gg/example", Cooldown = 5 },
                new() { Name = "!socials", Response = "Follow me on Twitter @example", Cooldown = 5 },
                new() { Name = "!uptime", Response = "Stream has been live for {uptime}", Cooldown = 10 }
            };
            SaveCommands();
        }
    }

    private void SaveCommands()
    {
        var json = JsonSerializer.Serialize(_commands, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("commands.json", json);
    }
}