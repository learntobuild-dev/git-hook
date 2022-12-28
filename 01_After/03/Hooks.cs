
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

    private static readonly Dictionary<string, string[]> Permissions = 
        new Dictionary<string, string[]>
        {
            {
                "refs/heads/master",
                new string[] {"timothy@learntobuild.dev"}
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
            case "pre-receive":
                return ExecutePreReceive(standardInput);
            case "update":
                return ExecuteUpdate(args[0], args[1], args[2]);
            case "post-receive":
                return ExecutePostReceive();
        }

        return Task.CompletedTask;
    }

    private static async Task ExecutePreCommit()
    {
        var result = await ProcessHelper.RunProcessAsync(
            "dotnet",
            "test -l trx -r TestResults",
            30000);

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

    private static async Task ExecutePreReceive(
        string standardInput)
    {
        var lines = Git.ParsePreReceiveInput(standardInput);

        foreach (var line in lines)
        {
            var commits =
                await Git.RevList(line.From, line.To);

            foreach (var commit in commits)
            {
                var commitMessage =
                    await Git.GetCommitMessage(commit);

                if (!HasTaskNumber(commitMessage))
                {
                    Console.WriteLine(
                        @$"Commit {commit} does not have a task number " +
                        @"associated with it");
                    Environment.Exit(-1);
                }
            }
        }
    }

    private static async Task ExecuteUpdate(
        string referenceName,
        string from,
        string to)
    {
        if (Permissions.ContainsKey(referenceName))
        {
            var allowedEmails = Permissions[referenceName];

            var commits = await Git.RevList(from, to);

            foreach (var commit in commits)
            {
                var authorEmail = await Git.GetEmail(commit);

                if (!allowedEmails.Any(
                        e => e.Equals(
                            authorEmail,
                            StringComparison.InvariantCultureIgnoreCase)))
                {
                    Console.WriteLine(
                        @$"User {authorEmail} is not allowed to " +
                        @$"commit to reference {referenceName}");
                    Environment.Exit(-1);
                }
            }
        }
        else
        {
            Console.WriteLine(
                $"Pushing to reference {referenceName} is denied");
            Environment.Exit(-1);
        }
    }

    private static async Task ExecutePostReceive()
    {
        var buildPath = "/tmp/build";

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        var repoPath = Path.Combine(buildPath, "calculator");

        if (Directory.Exists(repoPath))
        {
            Directory.Delete(repoPath, true);
        }

        await
            Git.Clone(
                "http://localhost:7000/githooks/calculator.git",
                buildPath);

        var buildResult =
            await
                ProcessHelper.RunProcessAsync(
                    "dotnet",
                    $"build",
                    30000,
                    repoPath);

        if (buildResult.ExitCode != 0)
        {
            throw new Exception(buildResult.Error);
        }
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