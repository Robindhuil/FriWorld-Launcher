namespace FriWorld.Launcher.Cli;

/// <summary>
/// Minimal argument parsing. A command name plus <c>--key value</c> pairs and <c>--flag</c>
/// switches is the whole grammar; a parsing library would be the heaviest dependency here.
/// </summary>
public sealed class CommandLineOptions
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    private CommandLineOptions(string command, IReadOnlyList<string> positional)
    {
        Command = command;
        Positional = positional;
    }

    public string Command { get; }

    public IReadOnlyList<string> Positional { get; }

    public static CommandLineOptions Parse(string[] args)
    {
        var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : "help";
        var positional = new List<string>();
        var options = new CommandLineOptions(command, positional);

        for (var i = command == "help" && (args.Length == 0 || args[0].StartsWith('-')) ? 0 : 1; i < args.Length; i++)
        {
            var arg = args[i];

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            var name = arg[2..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            options._values[name] = hasValue ? args[++i] : null;
        }

        return options;
    }

    public bool Has(string name) => _values.ContainsKey(name);

    public string? Value(string name) => _values.TryGetValue(name, out var value) ? value : null;

    public string Value(string name, string fallback) => Value(name) ?? fallback;
}
