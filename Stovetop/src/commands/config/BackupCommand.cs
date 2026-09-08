using Stovetop.Exceptions;
using Stovetop.stovetop;

namespace Stovetop.Commands.Config;

public class BackupCommand
{
    public static void Run()
    {
        string? subcommand = CommandRegistry.GetSubcommand("backup", "create");

        switch (subcommand?.ToLower())
        {
            case "list" or "ls" or "view":
                ListBackups();
                break;
            case "revert":
                HandleRevert();
                break;
            case "clean":
                CleanBackups();
                break;
            default:
                CreateBackup();
                break;
        }
    }

    private static void HandleRevert()
    {
        string? backupId = CommandRegistry.GetPositionalArgument("backup", 2);

        if (string.IsNullOrEmpty(backupId))
        {
            return;
        }

        string? resolvedBackupId = ResolveBackupId(backupId);

        if (string.IsNullOrEmpty(resolvedBackupId))
            throw new StovetopBackupNotFoundException();

        RevertToBackup(resolvedBackupId);
    }

    private static string? ResolveBackupId(string backupId) =>
        (backupId == "latest") ? GetLatestBackup() : backupId;

    private static string[]? BackupList()
    {
        if (StovetopCore.StovetopBackupRoot == null)
            return null;

        return Directory
            .GetFiles(StovetopCore.StovetopBackupRoot, "*-stovetop-backup.stove")
            .OrderByDescending(f => Path.GetFileName(f))
            .ToArray();
    }

    private static void ListBackups()
    {
        if (!Directory.Exists(StovetopCore.StovetopBackupRoot))
        {
            StovetopCore.StovetopLogger?.Warn("No backups directory found");
            return;
        }

        if (!HasBackups())
        {
            StovetopCore.StovetopLogger?.Info("No backups found");
            return;
        }

        Console.WriteLine($"[STOVE] You have {BackupList()!.Length} backup(s):\n");

        bool showInfo =
            CommandRegistry.GetPositionalArgument("backup", 2) == "-i"
            || CommandRegistry.GetPositionalArgument("backup", 2) == "--info";

        bool latestDone = false;

        foreach (var backup in BackupList()!)
        {
            string fileName = Path.GetFileName(backup);
            string backupId = fileName.Replace("-stovetop-backup.stove", "");

            if (showInfo)
            {
                var fileInfo = new FileInfo(backup);

                Console.Write(
                    $"\t{backupId} (Created: {ParseBackupDate(backupId)}, Size: {fileInfo.Length} bytes)"
                );
            }
            else
            {
                Console.Write($"\t{backupId}");
            }

            if (!latestDone)
            {
                Console.WriteLine(" (Latest)");
                latestDone = true;
            }
            else
                Console.WriteLine();
        }
    }

    private static string ParseBackupDate(string backupId)
    {
        string date = backupId.Replace("-stovetop-backup.stove", "");

        string[] dateParts = date.Split("-");
        string[] timeParts = dateParts[3].Split(":");

        string fullDate =
            $"{dateParts[2]}/{dateParts[1]}/{dateParts[0]} {timeParts[0]}:{timeParts[1]}:{timeParts[2]}";

        return fullDate;
    }

    public static void CreateBackup()
    {
        if (StovetopCore.StovetopBackupRoot == null)
            return;

        if (!StovetopCore.StovetopConfigExists)
            return;

        string backupPath = Path.Combine(
            StovetopCore.StovetopBackupRoot,
            $"{DateTime.Now:yyyy-MM-dd-HH:mm:ss}-stovetop-backup.stove"
        );

        File.Copy(StovetopCore.StovetopConfigPath, backupPath, true);
        StovetopCore.StovetopLogger?.Success($"Backup created: {backupPath}");
    }

    public static void RevertToBackup(string backupId)
    {
        if (StovetopCore.StovetopConfigRoot == null)
            return;

        string backupPath = Path.Combine(StovetopCore.StovetopConfigRoot, "cache/backups");
        string backupFile = Path.Combine(backupPath, $"{backupId}-stovetop-backup.stove");

        if (!File.Exists(backupFile))
        {
            StovetopCore.StovetopLogger?.Error($"Backup '{backupId}' not found");
            return;
        }

        try
        {
            StovetopCore.StovetopLogger?.Info($"Creating safety backup before reverting...");
            CreateBackup();

            StovetopCore.StovetopLogger?.Info($"Reverting to backup: {backupId}");
            if (StovetopCore.StovetopConfigPath != null)
                File.Copy(backupFile, StovetopCore.StovetopConfigPath, true);

            StovetopCore.StovetopLogger?.Success($"Successfully reverted to backup: {backupId}");
        }
        catch (Exception e)
        {
            StovetopCore.StovetopLogger?.Error($"Failed to revert: {e.Message}");
        }
    }

    // get the most recent backup
    public static string? GetLatestBackup()
    {
        if (StovetopCore.StovetopConfigRoot == null)
            return null;

        string backupPath = Path.Combine(StovetopCore.StovetopConfigRoot, "cache/backups");

        if (!Directory.Exists(backupPath))
            return null;

        var backups = Directory
            .GetFiles(backupPath, "*-stovetop-backup.stove")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToArray();

        if (backups.Length == 0)
            return null;

        string fileName = Path.GetFileName(backups[0]);
        return fileName.Replace("-stovetop-backup.stove", "");
    }

    public static bool HasBackups() =>
        Directory.Exists(StovetopCore.StovetopBackupRoot)
        && Directory.GetFiles(StovetopCore.StovetopBackupRoot, "*-stovetop-backup.stove").Length
            > 0;

    public static bool BackupExists(string backupId) =>
        StovetopCore.StovetopBackupRoot != null
        && File.Exists(
            Path.Combine(StovetopCore.StovetopBackupRoot, $"{backupId}-stovetop-backup.stove")
        );

    // TODO | Clean backups method, with numbered limit, max/min date etc.
    public static void CleanBackups()
    {
        string? flag = CommandRegistry.GetPositionalArgument("backup", 2);
        int amount = 0;

        if (!HasBackups())
        {
            StovetopCore.StovetopLogger?.Info("No backups to clean");
            return;
        }

        if (flag != null)
        {
            if (flag == "--all" || flag == "-a")
            {
                amount = int.MaxValue;
            }

            if (flag == "--amount" || flag == "--count")
            {
                amount = int.Parse(CommandRegistry.GetPositionalArgument("backup", 3) ?? "0");
            }
        }

        StovetopCore.StovetopLogger?.Info("Cleaning backups...");

        if (StovetopCore.StovetopBackupRoot == null) return;
    
        string[] backups = Directory
            .GetFiles(StovetopCore.StovetopBackupRoot, "*-stovetop-backup.stove")
            .OrderBy(f => File.GetCreationTime(f))
            .ToArray();

        int count = 0;
        foreach (var backup in backups)
        {
            File.Delete(backup);
            count++;
            if (count >= amount)
                break;
        }
    }
}
