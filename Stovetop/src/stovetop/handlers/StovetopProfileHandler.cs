using System;
using System.IO;
using Stovetop.Commands;
using Stovetop.ConfigParser;

namespace Stovetop.stovetop.handlers;

public class StovetopProfileHandler
{
    // Right SO the general idea of this class is
    // 1. it gets run within the run and build commands
    // 2. it checks whether theres a profile flag set in the argument array
    // 3. if there is one, itll find the index of the given profile
    // 4. itll override the loaded configs values with the values inside the profile if the profile exists and is verified

    // this will load the profile from the given name, and error if it doesnt exist
    public static void LoadProfile(string? profileName)
    {
        if (profileName == "default")
            return;
        
        ConfigModel profile = new ConfigModel();
        ConfigModel newConfig = new ConfigModel();
        
        string profilePath;
        try
        {
            profilePath = GetProfilePath(profileName);
        }
        catch (Exception ex)
        {
            StovetopCore.StovetopLogger?.Error("Failed to determine profile path: " + ex.Message);
            return;
        }
        
        if (!File.Exists(profilePath))
        {
            StovetopCore.StovetopLogger?.Error("Profile not found: " + profilePath);
            return;
        }

        profile = StovetopConfigParser.ParseFile(profilePath);

        if (StovetopCore.StovetopConfig == null)
        {
            StovetopCore.StovetopLogger?.Error("Base config is not loaded; cannot apply profile");
            return;
        }

        newConfig = MergeProfile(StovetopCore.StovetopConfig, profile);
        StovetopCore.StovetopConfig = newConfig;
    }

    // this will be used to merge the base config with the profile config, only overriding the values that match both the base config and profile config
    public static ConfigModel MergeProfile(ConfigModel baseConfig, ConfigModel profileConfig)
    {
        ConfigModel result = new ConfigModel();

        result = baseConfig;

        if (profileConfig.Commands.Count == 0 && profileConfig.Variables.Count == 0 && profileConfig.Aliases.Count == 0 && profileConfig.Hooks.Count == 0)
            return result;

        // Profile runtime verification
        if (!String.IsNullOrWhiteSpace(profileConfig.Runtime))
        {
            StovetopCore.StovetopRuntime = profileConfig.Runtime;
            
            if (!StovetopCore.VerifyRuntime())
            {
                StovetopCore.StovetopLogger?.Error("Provided runtime in profile does not exist... Falling back to base config");
                result.Runtime = baseConfig.Runtime;
                
                return result;
            }
            
            result.Runtime = profileConfig.Runtime;
        }

        foreach (var cmd in profileConfig.Commands)
        {
            baseConfig.Commands[cmd.Key] = cmd.Value;
        }

        foreach (var var in profileConfig.Variables)
        {
            baseConfig.Variables[var.Key] = var.Value;
        }
        
        foreach (var alias in profileConfig.Aliases)
        {
            baseConfig.Aliases[alias.Key] = alias.Value;
        }
        
        foreach (var hooks in profileConfig.Hooks)
        {
            baseConfig.Hooks[hooks.Key] = hooks.Value;
        }

        return result; // return the merged profile
    }

    // this will check whether the given profile exists
    public static bool ProfileExists(string profileName)
    {
        bool verdict = false;

        if (File.Exists(GetProfilePath(profileName)))
            verdict = true;

        return verdict;
    }

    // this will return the full path to the profile file
    public static string GetProfilePath(string profileName)
    {
        if (String.IsNullOrWhiteSpace(StovetopCore.StovetopProfileRoot))
            throw new InvalidOperationException("Profile root is not initialized");

        return Path.Combine(StovetopCore.StovetopProfileRoot, profileName + ".stove");
    }

    // list profile method
    public static void ListProfiles() { }

    public static bool HasProfileFlag()
    {
        return CommandRegistry.HasProfileFlag();
    }

    public static string? GetProfileFlagValue()
    {
        if (CommandRegistry.CurrentArgs == null)
            return null;

        foreach (var flag in new[] { "--profile", "-p" })
        {
            var value = CommandRegistry.GetFlagValue(flag);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
