using WinRecorder.Tests;

namespace WinRecorder.Tests;

internal static class UnitTest1
{
    public static int Main()
    {
        try
        {
            EntryFormatterTests.RunAll();
            EventDeduplicatorTests.RunAll();
            Console.WriteLine("EntryFormatterTests + EventDeduplicatorTests: OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("EntryFormatterTests: FAILED");
            return 1;
        }
    }
}