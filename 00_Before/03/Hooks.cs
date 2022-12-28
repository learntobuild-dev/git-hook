
static class Hooks
{
    private static readonly Dictionary<string, string> Tasks = 
        new Dictionary<string, string>
        {
            {
                "timothy@learntobuild.dev",
                "Implementing the Addition Operator"
            },
            {
                "test@test.com",
                "merging branches"
            }
        };

    public static Task ExecuteHook(
        string hookName,
        string[] args,
        string standardInput)
    {
        switch (hookName)
        {
            case "pre-commit":
                return ExecutePreCommit();
            case "prepare-commit-msg":
                return ExecutePrepareCommitMsg(args[0]);
            case "commit-msg":
                return ExecuteCommitMsg(args[0]);
            case "post-checkout":
                ExecutePostCheckout();
                break;
            case "pre-rebase":
                ExecutePreRebase();
                break;
        }

        return Task.CompletedTask;
    }

    private static async Task ExecutePreCommit()
    {
        var result = await ProcessHelper.RunProcessAsync(
            "dotnet",
            "test -l trx -r TestResults",
            10000);

        if (result.ExitCode != 0)
        {
            Environment.Exit(result.ExitCode);
        }
    }

    private static async Task ExecutePrepareCommitMsg(
        string commitEditMsgFilePath)
    {
        var email = Environment.GetEnvironmentVariable(
            "GIT_AUTHOR_EMAIL");

        if (email != null)
        {
            await File.WriteAllTextAsync(
                commitEditMsgFilePath,
                Tasks[email]);
        }
    }

    private static async Task ExecuteCommitMsg(
        string commitEditMsgFilePath)
    {
        var commitMsg = await File.ReadAllTextAsync(commitEditMsgFilePath);
        if (!HasTaskNumber(commitMsg))
        {
            Environment.Exit(-1);
        }
    }

    private static void ExecutePostCheckout()
    {
        var files =
            Directory.GetFiles(Environment.CurrentDirectory);

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);

            if (fileName.StartsWith("pre-commit") ||
                fileName.StartsWith("prepare-commit-msg") ||
                fileName.StartsWith("commit-msg"))
            {
                File.Delete(file);
            }
        }

        var testResultsPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "TestResults");

        if (Directory.Exists(testResultsPath))
        {
            Directory.Delete(testResultsPath, true);
        }

        var hooksTraceFilePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "hooks-trace.log");

        if (File.Exists(hooksTraceFilePath))
        {
            File.Delete(hooksTraceFilePath);
        }
    }

    private static void ExecutePreRebase()
    {
        Environment.Exit(-1);
    }

    private static bool HasTaskNumber(
        string commitMessage)
    {
        var hashSignIndex = commitMessage.IndexOf('#');
        if (hashSignIndex == -1)
        {
            return false;
        }
        var firstSpaceAfterHashSign = commitMessage.IndexOf(' ', hashSignIndex);
        if (firstSpaceAfterHashSign == -1)
        {
            firstSpaceAfterHashSign = commitMessage.Length - 1;
        }
        var length = firstSpaceAfterHashSign - hashSignIndex - 1;
        var ticketNumber = commitMessage.Substring(hashSignIndex + 1, length + 1);
        return int.TryParse(ticketNumber, out _);
    }
}