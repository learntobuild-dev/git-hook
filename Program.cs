
string ReadStandardInput()
{
    var inputStream = Console.OpenStandardInput();
    var reader = new StreamReader(inputStream);
    return reader.ReadToEnd();
}

var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

var standardInput = ReadStandardInput();

await HookOutputHelper.WriteHookInfo(processName, args, standardInput);

await Hooks.ExecuteHook(processName, args, standardInput);