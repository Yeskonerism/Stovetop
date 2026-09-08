using System.Diagnostics;
using System.Text;
using Stovetop.Commands;
using Stovetop.ConfigParser;
using Stovetop.Exceptions;
using Stovetop.stovetop.handlers;
using static System.Environment;

namespace Stovetop.stovetop;

public static class StovetopCore
{
    public static string? StovetopConfigRoot;
    public static string? StovetopConfigPath;
    public static bool StovetopConfigExists;
    public static ConfigModel? StovetopConfig;
    public static StovetopLogger? StovetopLogger;
    public static string? StovetopRuntime;

    public static string? StovetopBackupRoot;
    public static string? StovetopProfileRoot;

    public static bool RunSilent;
    public static bool RunVerbose;
    public static bool RunHookless;

    public static void Initialize(bool ignoreConfig = false)
    {
        string stovetopRoot = Directory.GetCurrentDirectory();
        StovetopConfigRoot = Path.Combine(stovetopRoot, StovetopConstants.ConfigDirName);
        StovetopConfigPath = Path.Combine(StovetopConfigRoot, StovetopConstants.ConfigFileName);

        StovetopBackupRoot = Path.Combine(StovetopConfigRoot, StovetopConstants.ConfigBackupFolder);
        StovetopProfileRoot = Path.Combine(StovetopConfigRoot, StovetopConstants.ConfigProfileFolder);

        if (CommandRegistry.CurrentArgs != null)
        {
            RunSilent = CommandRegistry.CurrentArgs.Contains(StovetopConstants.SilentFlag);
            RunVerbose = CommandRegistry.CurrentArgs.Contains(StovetopConstants.VerboseFlag);
            RunHookless = CommandRegistry.CurrentArgs.Contains(StovetopConstants.NoHooksFlag);
        }

        SetupLogger();

        if (ignoreConfig)
            return;

        if (!VerifyConfig())
        {
            StovetopLogger?.Error("No config found");
            return;
        }

        LoadConfig();

        var profileName = StovetopProfileHandler.GetProfileFlagValue();
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            StovetopProfileHandler.LoadProfile(profileName);
        }

        StovetopRuntime = StovetopConfig?.Runtime;
    }

    public static bool VerifyConfig(bool ignoreConfig = false)
    {
        StovetopConfigExists = File.Exists(StovetopConfigPath);

        if (!ignoreConfig)
            StovetopLogger?.Info(
                StovetopConfigExists ? "Main Config Verified" : "Main Config Not Verified"
            );

        return StovetopConfigExists;
    }

    public static void LoadConfig()
    {
        if (StovetopConfigPath == null)
            throw new StovetopUninitialisedException();

        StovetopConfig = StovetopConfigParser.ParseFile(StovetopConfigPath);
    }

    public static void SaveConfig()
    {
        if (StovetopConfig == null || StovetopConfigPath == null)
            throw new StovetopUninitialisedException();

        var sb = new StringBuilder();

        // Write project info
        sb.AppendLine($"// Stovetop configuration file");
        sb.AppendLine($"project(\"{StovetopConfig.Project}\")");
        sb.AppendLine($"version({StovetopConfig.Version})");
        sb.AppendLine();

        // Write runtime
        sb.AppendLine($"runtime({StovetopConfig.Runtime})");
        sb.AppendLine();

        // Write variables
        if (StovetopConfig.Variables.Count > 0)
        {
            sb.AppendLine("// Variables");
            foreach (var variable in StovetopConfig.Variables)
            {
                sb.AppendLine($"var {variable.Key} = {variable.Value}");
            }
            sb.AppendLine();
        }

        // Write commands
        sb.AppendLine("// Commands");
        foreach (var command in StovetopConfig.Commands)
        {
            string appendCommand = command.Key == "executable" ? "" : "_command";
            sb.AppendLine($"{command.Key}{appendCommand}(\"{command.Value}\")");
        }
        sb.AppendLine();

        // Write aliases
        if (StovetopConfig.Aliases.Count > 0)
        {
            sb.AppendLine("// Aliases");
            foreach (var alias in StovetopConfig.Aliases)
            {
                sb.AppendLine($"alias(\"{alias.Key}\", \"{alias.Value}\")");
            }
        }

        File.WriteAllText(StovetopConfigPath, sb.ToString());
        StovetopLogger?.Success("Configuration saved successfully.");
    }

    public static void SetupLogger()
    {
        StovetopLogger = new StovetopLogger();
        StovetopLogger.Info("Logger initialized");
    }

    public static void CreateDefaultStructure()
    {
        if (StovetopConfigRoot == null)
            throw new StovetopUninitialisedException();

        Directory.CreateDirectory(StovetopConfigRoot);

        foreach (var subDirectory in new[] { "profiles", "cache", "cache/backups", "scripts" })
            Directory.CreateDirectory(Path.Combine(StovetopConfigRoot, subDirectory));
    }

    public static bool VerifyRuntime()
    {
        if (StovetopRuntime == null)
            throw new StovetopNonexistentRuntimeException();
        
        ProcessStartInfo startInfo = new()
        {
            Arguments = StovetopRuntime,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.FileName = (OSVersion.Platform == PlatformID.Unix) ? "which" : "where";

        using (Process? process = Process.Start(startInfo))
        {
            process?.WaitForExit();
            if (process != null && process.ExitCode != 0)
            {
                throw new StovetopNonexistentRuntimeException();
            }
        }

        return true;
    }
}
