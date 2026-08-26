namespace FriWorld.Launcher.Cli;

/// <summary>
/// Minimal argument parsing. A command name plus <c>--key value</c> pairs and <c>--flag</c>
/// switches is the whole grammar; a parsing library would be the heaviest dependency here.
/// An option may repeat, which is how <c>--exec</c> takes one entry per platform.
/// </summary>
public sealed class CommandLineOptions
{
    private readonly Dictionary<string, List<string?>> _values = new(StringComparer.OrdinalIgnoreCase);

    private CommandLineOptions(string command, IReadOnlyList<string> positional)
    {
        Command = command;
        Positional = positional;
    }

    public string Command { get; }

    public IReadOnlyList<string> Positional { get; }

    public static CommandLineOptions Parse(string[] args)
    {
        var hasCommand = args.Length > 0 && !args[0].StartsWith('-');
        var command = hasCommand ? args[0] : "help";

        var positional = new List<string>();
        var options = new CommandLineOptions(command, positional);

        for (var i = hasCommand ? 1 : 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            var name = arg[2..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);

            if (!options._values.TryGetValue(name, out var list))
            {
                list = [];
                options._values[name] = list;
            }

            list.Add(hasValue ? args[++i] : null);
        }

        return options;
    }

    public bool Has(string name) => _values.ContainsKey(name);

    /// <summary>The last value given for an option, or null when it was absent or valueless.</summary>
    public string? Value(string name) =>
        _values.TryGetValue(name, out var list) ? list[^1] : null;

    public string Value(string name, string fallback) => Value(name) ?? fallback;

    /// <summary>Every value given for a repeatable option, in order.</summary>
    public IReadOnlyList<string> Values(string name) =>
        _values.TryGetValue(name, out var list) ? list.OfType<string>().ToList() : [];
}
