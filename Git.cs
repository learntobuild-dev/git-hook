using System.Diagnostics;
using System.Text;

static class Git
{
    public static PreReceiveInputLine[] ParsePreReceiveInput(string input)
    {
        var result = new List<PreReceiveInputLine>();

        var lines = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var arguments = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (arguments.Length != 3)
            {
                throw new Exception("Invalid input length");
            }

            result.Add(new PreReceiveInputLine(arguments[2].TrimEnd(), arguments[0], arguments[1]));
        }
        
        return result.ToArray();
    }

    public static async Task<string[]> RevList(string from, string to)
    {
        if (from.All(c => c == '0'))
        {
            return new[] { to };
        }

        var result = await ProcessHelper.RunProcessAsync("git", $"rev-list {from}..{to}", 5000);

        if (result.ExitCode != 0)
        {
            throw new Exception(result.Error);
        }

        return result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    public static async Task<string> GetEmail(string commitHash)
    {
        var result = await ProcessHelper.RunProcessAsync("git", $"show -s --format='%ae' {commitHash}", 5000);

        if (result.ExitCode != 0)
        {
            throw new Exception(result.Error);
        }

        return result.Output;
    }

    public static async Task<string> GetCommitMessage(string commitHash)
    {
        var result = await ProcessHelper.RunProcessAsync("git", $"log -n 1 --pretty=format:%s {commitHash}", 5000);

        if (result.ExitCode != 0)
        {
            throw new Exception(result.Error);
        }

        return result.Output;
    }
}

class PreReceiveInputLine
{
    public PreReceiveInputLine(string refName, string from, string to)
    {
        RefName = refName;
        From = from;
        To = to;
    }

    public string RefName { get; }
    public string From { get; }
    public string To { get; }
}

static class ProcessHelper
{
    public static async Task<ProcessResult> RunProcessAsync(
        string command,
        string arguments,
        int timeout)
    {
        var result = new ProcessResult();

        using var process = new Process();

        process.StartInfo.FileName = command;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new StringBuilder();
        var outputCloseEvent = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data == null)
            {
                outputCloseEvent.SetResult(true);
            }
            else
            {
                outputBuilder.Append(e.Data);
                outputBuilder.Append('\n');
            }
        };

        var errorBuilder = new StringBuilder();
        var errorCloseEvent = new TaskCompletionSource<bool>();

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data == null)
            {
                errorCloseEvent.SetResult(true);
            }
            else
            {
                errorBuilder.Append(e.Data);
                errorBuilder.Append('\n');
            }
        };

        var isStarted = process.Start();
        if (!isStarted)
        {
            result.ExitCode = process.ExitCode;
            return result;
        }

        // Reads the output stream first and then waits because deadlocks are possible
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Creates task to wait for process exit using timeout
        var waitForExit = Task.Run(() => process.WaitForExit(timeout));

        // Create task to wait for process exit and closing all output streams
        var processTask = Task.WhenAll(waitForExit, outputCloseEvent.Task, errorCloseEvent.Task);

        // Waits process completion and then checks it was not completed by timeout
        if (await Task.WhenAny(Task.Delay(timeout), processTask) == processTask && waitForExit.Result)
        {
            result.ExitCode = process.ExitCode;
            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
        }
        else
        {
            try
            {
                // Kill hung process
                process.Kill();
            }
            catch
            {
                // ignored
            }

            result.ExitCode = 1;
            result.Error = $"Timeout waiting for command {command} {arguments}";
        }

        return result;
    }
}

struct ProcessResult
{
    public int ExitCode;
    public string Output;
    public string Error;
}