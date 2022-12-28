
static class Hooks
{
    public static Task ExecuteHook(
        string hookName,
        string[] args,
        string standardInput)
    {
        return Task.CompletedTask;
    }
}