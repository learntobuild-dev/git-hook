
using System.Collections;

static class HookOutputHelper
{
    public static async Task WriteHookInfo(string processName, string[] args, string standardInput)
    {
        var now = System.DateTime.Now.ToString("HH:mm:ss.ffff");

        await File.AppendAllTextAsync("hooks-trace.log", $"[{now}] {processName}{Environment.NewLine}");

        await File.WriteAllTextAsync($"{processName}-{DateTime.Now:HH-mm-ss-ffff}", BuildInfo(args, standardInput));
    }

    private static string BuildInfo(string[] args, string standardInput)
    {
        var sb = new System.Text.StringBuilder();
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            if (variable.Key is string key && variable.Value is string value && key.StartsWith("GIT_"))
            {
                sb.AppendLine($"{key}={value}");
            }
        }
        sb.AppendLine();
        sb.AppendJoin(", ", args);
        sb.AppendLine();
        sb.AppendLine(standardInput);
        return sb.ToString();
    }
}