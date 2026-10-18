using System.Text;
using System.Text.Json;

namespace BoundaryTransfer.Tests;

internal sealed record AlgorithmCase(string Id, string Category, Action Body);

internal static class Program
{
    private static int Main(string[] args)
    {
        string logPath = Path.GetFullPath("result.log");
        if (args.Length == 2 && args[0] == "--log")
            logPath = Path.GetFullPath(args[1]);
        else if (args.Length != 0)
        {
            Console.Error.WriteLine("用法：BoundaryTransfer.Tests [--log <JSONL日志路径>]");
            return 2;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            using var log = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
            var cases = new List<AlgorithmCase>();
            RoutingAndConfigTests.Add(cases);
            TouchBoundaryTests.Add(cases);
            DirectLandingTests.Add(cases);
            CoastalTests.Add(cases);
            LandingTests.Add(cases);
            FallbackLandingTests.Add(cases);
            HiddenCommandTests.Add(cases);
            if (cases.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != cases.Count)
                throw new InvalidOperationException("测试 ID 重复。");

            Emit(log, new
            {
                type = "suite", schemaVersion = 1, name = "BoundaryTransfer.Tests", targetFramework = "net9.0",
                runtime = Environment.Version.ToString(), scope = "pure-algorithms", deterministicSeed = 314159,
                clientNetworkVerified = false, realTerrariaTileMetadataVerified = false,
                testCount = cases.Count
            });
            int passed = 0, failed = 0;
            foreach (AlgorithmCase test in cases)
            {
                int before = Check.Assertions;
                try
                {
                    test.Body();
                    passed++;
                    Emit(log, new { type = "case", id = test.Id, category = test.Category, status = "passed", assertions = Check.Assertions - before });
                    Console.WriteLine($"[PASS] {test.Id}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Emit(log, new
                    {
                        type = "case", id = test.Id, category = test.Category, status = "failed",
                        assertions = Check.Assertions - before, exception = ex.GetType().FullName, error = ex.Message,
                        stackTrace = ex.StackTrace
                    });
                    Console.Error.WriteLine($"[FAIL] {test.Id}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            int exitCode = failed == 0 ? 0 : 1;
            Emit(log, new { type = "summary", total = cases.Count, passed, failed, skipped = 0, assertions = Check.Assertions, exitCode });
            Console.WriteLine($"TOTAL={cases.Count} PASSED={passed} FAILED={failed} ASSERTIONS={Check.Assertions} EXIT_CODE={exitCode}");
            Console.WriteLine($"RESULT_LOG={logPath}");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"测试宿主/日志初始化失败：{ex}");
            return 2;
        }
    }

    private static void Emit(StreamWriter log, object value) => log.WriteLine(JsonSerializer.Serialize(value));
}

internal static class Check
{
    public static int Assertions { get; private set; }

    public static void True(bool actual, string message = "应为 true")
    {
        Assertions++;
        if (!actual) throw new InvalidOperationException(message);
    }

    public static void False(bool actual, string message = "应为 false") => True(!actual, message);

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        Assertions++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message ?? "结果不符"}；expected={expected}; actual={actual}");
    }

    public static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string? message = null)
    {
        Assertions++;
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException(message ?? "序列不符");
    }

    public static TException Throws<TException>(Action action, string? messageFragment = null) where TException : Exception
    {
        Assertions++;
        try { action(); }
        catch (TException ex)
        {
            if (messageFragment is not null && !ex.Message.Contains(messageFragment, StringComparison.Ordinal))
                throw new InvalidOperationException($"异常消息未包含 {messageFragment}：{ex.Message}");
            return ex;
        }
        throw new InvalidOperationException($"应抛出 {typeof(TException).Name}");
    }
}

internal static class Cases
{
    public static void Add(this List<AlgorithmCase> cases, string id, string category, Action body) => cases.Add(new(id, category, body));
}
