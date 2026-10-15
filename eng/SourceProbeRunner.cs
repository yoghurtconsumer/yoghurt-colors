using System.Reflection;
using Xunit;

// Only the isolated SDK-8 probe includes this entry point. Normal production test
// projects still use VSTest/xUnit. These tests use Fact/Theory + InlineData only;
// fail explicitly if a future test needs fixtures or another data discoverer.
internal static class SourceProbeRunner
{
    private static async Task<int> Main()
    {
        var passed = 0; var failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().OrderBy(t => t.FullName))
        foreach (var method in type.GetMethods().OrderBy(m => m.Name))
        {
            var fact = method.GetCustomAttribute<FactAttribute>();
            if (fact is null) continue;
            if (fact.Skip is not null) { Console.WriteLine($"FAIL unsupported skipped probe test {type.Name}.{method.Name}"); failed++; continue; }
            var data = method.GetCustomAttributes<InlineDataAttribute>().SelectMany(a => a.GetData(method)).ToArray();
            if (fact is TheoryAttribute && data.Length == 0) throw new InvalidOperationException("Probe supports InlineData theories only.");
            if (fact is not TheoryAttribute) data = [Array.Empty<object>()];
            foreach (var arguments in data)
            {
                var name = $"{type.Name}.{method.Name}({string.Join(",", arguments.Select(a => a?.ToString() ?? "null"))})";
                object? instance = null;
                try
                {
                    instance = Activator.CreateInstance(type);
                    var result = method.Invoke(instance, arguments);
                    if (result is Task task) await task;
                    if (result is ValueTask valueTask) await valueTask;
                    Console.WriteLine($"PASS {name}"); passed++;
                }
                catch (Exception e)
                {
                    var error = e is TargetInvocationException { InnerException: not null } invocation ? invocation.InnerException : e;
                    Console.WriteLine($"FAIL {name}: {error}"); failed++;
                }
                finally { (instance as IDisposable)?.Dispose(); }
            }
        }
        Console.WriteLine($"Source probe: {passed} passed, {failed} failed. No hardware providers invoked.");
        return failed == 0 && passed > 0 ? 0 : 1;
    }
}
