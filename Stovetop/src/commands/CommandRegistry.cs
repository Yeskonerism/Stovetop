using Stovetop.Commands.Config;
using Stovetop.Commands.Pipeline;
using Stovetop.commands.user;
using Stovetop.stovetop;

namespace Stovetop.Commands;

public class CommandRegistry
{
    public static List<StovetopCommand> Commands { get; private set; }
    public static string[]? CurrentArgs;

    static CommandRegistry()
    {
        // initialise command list
        Commands = new List<StovetopCommand>();

        // register all commands
        RegisterCommand(
            "init",
            "Initialize a new project",
            "stove init (runtime)",
            StovetopCommand.CommandCatagory.Config,
            InitCommand.Run,
            ["i"]
        );
        RegisterCommand(
            "run",
            "Run the project",
            "stove run (--backup [backup])",
            StovetopCommand.CommandCatagory.Pipeline,
            RunCommand.Run,
            ["r"]
        );
        RegisterCommand(
            "build",
            "Build the project",
            "stove build (--backup [backup])",
            StovetopCommand.CommandCatagory.Pipeline,
            BuildCommand.Run,
            ["b", "bld"]
        );
        RegisterCommand(
            "backup",
            "Create a backup of the current config",
            "stove backup (list | revert [backup])",
            StovetopCommand.CommandCatagory.Config,
            BackupCommand.Run,
            ["bak", "bkp"]
        );
        RegisterCommand(
            "help",
            "Show this help message",
            "stove help (command)",
            StovetopCommand.CommandCatagory.User,
            HelpCommand.Run,
            ["h"]
        );
        RegisterCommand(
            "config",
            "View and edit the current config",
            "stove config (view|edit)",
            StovetopCommand.CommandCatagory.Config,
            ConfigCommand.Run,
            ["cfg"]
        );
        RegisterCommand("script", "", "", StovetopCommand.CommandCatagory.User, ScriptCommand.Run, ["sc"]);
    }

    public static void RegisterCommand(
        string name,
        string desc,
        string usage,
        StovetopCommand.CommandCatagory category,
        Action command,
        string[]? aliases = null
    )
    {
        string[] aliasVerification = aliases ?? Array.Empty<string>();

        Commands.Add(
            new StovetopCommand
            {
                Name = name,
                Description = desc,
                Usage = usage,
                Category = category,
                Command = command,
                Aliases = aliasVerification,
            }
        );
    }

    public static void PassArguments(string[] args)
    {
        CurrentArgs = args;
    }

    // command getting and fetching
    public static StovetopCommand? GetCommand(string? name) => GetCommandByNameOrAlias(name);

    private static StovetopCommand? GetCommandByNameOrAlias(string? name)
    {
        foreach (var command in Commands)
        {
            if (name != null && MatchesCommand(command, name))
                return command;
        }

        return null;
    }

    private static bool MatchesCommand(StovetopCommand command, string name)
    {
        if (command.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            return true;
    
        return command.Aliases?.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    public static int GetCommandIndex(string commandName)
    {
        if (CurrentArgs == null)
            return -1;

        for (int i = 0; i < CurrentArgs.Length; i++)
        {
            // check for both exact name match or alias (GetCommand already handles aliases)
            if (IsCommandAtIndex(i, commandName))
                return i;
        }

        return -1; // Not found
    }

    private static bool IsCommandAtIndex(int index, string commandName) =>
        GetCommand(CurrentArgs?[index])?.Name == commandName;

    public static string? GetPositionalArgument(string commandName, int position)
    {
        int commandIndex = GetCommandIndex(commandName);

        if (commandIndex == -1)
            return null;

        int targetIndex = commandIndex + position;

        if (CurrentArgs != null && targetIndex >= CurrentArgs.Length)
            return null;

        return CurrentArgs?[targetIndex];
    }

    public static string? GetSubcommand(string commandName, string? defaultValue = null)
    {
        int commandIndex = GetCommandIndex(commandName);

        if (commandIndex >= 0 && CurrentArgs != null && CurrentArgs.Length > commandIndex + 1)
            return CurrentArgs[commandIndex + 1];

        return defaultValue;
    }

    public static int GetFlagPosition(string flag)
    {
        if (CurrentArgs == null)
            return -1;

        int position = 0;

        foreach (var f in CurrentArgs)
        {
            if (f == flag)
            {
                return position;
            }

            position++;
        }

        return -1;
    }

    public static bool HasFlag(string flag)
    {
        return CurrentArgs != null && CurrentArgs.Contains(flag);
    }

    public static bool HasProfileFlag()
    {
        return HasFlag("--profile") || HasFlag("-p");
    }

    public static string? GetFlagValue(string flag)
    {
        int flagPosition = GetFlagPosition(flag);

        if (flagPosition == -1)
            return null;
        if (CurrentArgs == null)
            return null;
        if (CurrentArgs.Length <= flagPosition + 1)
            return null;

        return CurrentArgs[flagPosition + 1];
    }
}
