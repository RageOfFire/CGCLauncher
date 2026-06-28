using System.Runtime.InteropServices;
using CGCLauncher.Models;

namespace CGCLauncher.Utils;

public static class OsUtils
{
    public static string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx";
        return "linux";
    }

    public static string GetArch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x86_64",
        };
    }

    /// <summary>
    /// Evaluates a Mojang-style rules list (used by both libraries and launch arguments)
    /// against the current OS and an optional set of feature flags.
    /// </summary>
    public static bool EvaluateRules(List<Rule>? rules, Dictionary<string, bool>? features = null)
    {
        if (rules == null || rules.Count == 0) return true;

        // Default is disallowed once any rule exists; each matching rule sets the
        // running verdict, last match wins - mirrors Mojang's own evaluation order.
        bool allowed = false;

        foreach (var rule in rules)
        {
            bool matches = true;

            if (rule.Os != null)
            {
                if (!string.IsNullOrEmpty(rule.Os.Name) && rule.Os.Name != GetOsName())
                    matches = false;
                if (!string.IsNullOrEmpty(rule.Os.Arch) && rule.Os.Arch != GetArch())
                    matches = false;
            }

            if (rule.Features != null)
            {
                foreach (var (key, expected) in rule.Features)
                {
                    bool actual = features != null && features.TryGetValue(key, out var v) && v;
                    if (actual != expected) matches = false;
                }
            }

            if (matches)
                allowed = rule.Action == "allow";
        }

        return allowed;
    }
}
