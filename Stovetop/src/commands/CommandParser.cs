using System.Diagnostics;
using Stovetop.Commands.Config;
using Stovetop.Exceptions;
using Stovetop.stovetop;
using Stovetop.stovetop.handlers;

namespace Stovetop.Commands;

public class CommandParser
{
    public static void ParseCommands(string[] args, bool ignoreConfig = false)
    {
        // Only load config here if it hasn't already been loaded by Initialize
        if (args.Length > 0 && args[0] != "init" && !ignoreConfig)
        {
            if (StovetopCore.StovetopConfig == null)
            {
                StovetopCore.LoadConfig();

                if (!StovetopCore.StovetopConfigExists)
                    throw new StovetopNonexistentConfigException();
            }
        }

        if (args.Length == 0)
        {
            var helpCommand = CommandRegistry.GetCommand("help");
            helpCommand?.Command.Invoke();
            return;
        }

        // Only check the first argument (the command) - this will change later to support multiple command calls
        string commandName = args[0].ToLower();

        foreach (var command in CommandRegistry.Commands)
        {
            if (
                command.Aliases != null
                && (command.Name == commandName || command.Aliases.Contains(commandName))
            )
            {
                if(command.Category == StovetopCommand.CommandCatagory.Pipeline)
                    StovetopCore.VerifyRuntime();
                
                if (SupportsBackupFlag(command.Name))
                    ExecuteWithOptionalBackup(
                        command,
                        GetBackupFlagIndex() != -1 ? GetBackupFlagIndex() : 1
                    );
                else
                    command.Command.Invoke();
                return;
            }
        }

        if (
            StovetopCore.StovetopConfig != null
            && StovetopCore.StovetopConfig.Aliases.ContainsKey(commandName)
        )
        {
            string resolvedCommand = StovetopCore.StovetopConfig.Aliases[commandName];
            StovetopCore.StovetopLogger?.Info(
                $"Alias '{commandName}' resolved to '{resolvedCommand}'"
            );
            ExecuteShellCommand(resolvedCommand);
            return;
        }

        // If we get here, command not found
        StovetopCore.StovetopLogger?.Error($"Unknown command: {commandName}");
    }

    private static int GetBackupFlagIndex()
    {
        if (CommandRegistry.CurrentArgs != null)
            for (int i = 0; i < CommandRegistry.CurrentArgs.Length; i++)
            {
                if (CommandRegistry.CurrentArgs[i] == "--backup")
                    return i;
            }

        return -1;
    }

    // parse command specific arguments from config
    public static string[] ParseArguments(string? argString)
    {
        List<string> args = new List<string>();
        string currentToken = "";
        bool inQuotes = false;

        if (argString != null)
            foreach (char c in argString)
            {
                if (c == '"' || c == '\'')
                {
                    inQuotes = !inQuotes; // toggle
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(currentToken))
                    {
                        args.Add(currentToken);
                        currentToken = "";
                    }
                }
                else
                {
                    currentToken += c;
                }
            }

        if (!string.IsNullOrEmpty(currentToken))
            args.Add(currentToken);

        return args.ToArray();
    }

    private static void ExecuteShellCommand(string command)
    {
        string commandVariableSupport = StovetopVariableHandler.SubstituteVariables(command);

        var process = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{commandVariableSupport}\"",
            UseShellExecute = false,
        };

        var proc = Process.Start(process);
        proc?.WaitForExit();
    }

    // Helper method to execute run/build commands with optional backup config
    private static void ExecuteWithOptionalBackup(
        StovetopCommand command,
        int positionalArgumentIndex = 1
    )
    {
        if (
            CommandRegistry.GetPositionalArgument(command.Name, positionalArgumentIndex)
            != "--backup"
        )
        {
            command.Command.Invoke();
            return;
        }

        string? positionalArgument = CommandRegistry.GetPositionalArgument(
            command.Name,
            positionalArgumentIndex + 1
        );

        // Handle missing backup ID
        if (string.IsNullOrEmpty(positionalArgument))
        {
            StovetopCore.StovetopLogger?.Error(
                "Please specify a backup ID or 'latest' (e.g., stove run --backup latest)"
            );
            return;
        }

        // Resolve "latest" to actual backup ID
        string? backupId =
            positionalArgument == "latest" ? BackupCommand.GetLatestBackup() : positionalArgument;
        if (!BackupCommand.BackupExists(backupId!))
            throw new StovetopBackupNotFoundException(backupId!);

        // Validate backup root is initialized
        if (string.IsNullOrEmpty(StovetopCore.StovetopBackupRoot))
            throw new StovetopUninitialisedException();

        ExecuteBackupCommand(command, backupId!);
    }

    private static void ExecuteBackupCommand(StovetopCommand command, string backupId)
    {
        // Build path to back up config
        string pathToBackupConfig = Path.Combine(
            StovetopCore.StovetopBackupRoot,
            $"{backupId}-stovetop-backup.json"
        );
        string? originalConfigPath = StovetopCore.StovetopConfigPath;

        try
        {
            // Temporarily swap to back up config
            StovetopCore.StovetopConfigPath = pathToBackupConfig;
            StovetopCore.StovetopLogger?.Info($"Using backup config: {backupId}");
            StovetopCore.LoadConfig();
            StovetopCore.StovetopRuntime = StovetopCore.StovetopConfig?.Runtime;

            // Execute the command
            command.Command.Invoke();
        }
        finally
        {
            // Always restore original config path and reload
            StovetopCore.StovetopConfigPath = originalConfigPath;
            StovetopCore.LoadConfig();
        }
    }

    // Helper method to check if a command supports the --backup flag
    private static bool SupportsBackupFlag(string commandName)
    {
        return commandName == "run" || commandName == "build" || commandName == "config";
    }
}
