using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyAll();
        VerifyAny();
        VerifyNotAndHealthRemoval();
        VerifyCompositionAndTrace();
        VerifyEmptyGroupIdentities();
        VerifyRepeatability();
        Console.WriteLine($"KLEP Lock smoke passed: {assertions} assertions.");
    }

    private static void VerifyAll()
    {
        var keys = new FakeKeySource();
        var lockDefinition = MakeLock(new KLEPAll(
            new KLEPKeyPresent("key.a"),
            new KLEPKeyPresent("key.b")));

        Expect(!lockDefinition.Evaluate(keys).IsSatisfied, "All blocks with no keys");
        keys.Add("key.a");
        Expect(!lockDefinition.Evaluate(keys).IsSatisfied, "All blocks with one key missing");
        keys.Add("key.b");
        Expect(lockDefinition.Evaluate(keys).IsSatisfied, "All opens with every key");
    }

    private static void VerifyAny()
    {
        var keys = new FakeKeySource();
        var lockDefinition = MakeLock(new KLEPAny(
            new KLEPKeyPresent("key.a"),
            new KLEPKeyPresent("key.b")));

        Expect(!lockDefinition.Evaluate(keys).IsSatisfied, "Any blocks with no keys");
        keys.Add("key.b");
        Expect(lockDefinition.Evaluate(keys).IsSatisfied, "Any opens with one key");
    }

    private static void VerifyNotAndHealthRemoval()
    {
        var keys = new FakeKeySource();
        keys.Add("key.health");
        var deathLock = new KLEPLock(
            "lock.run-death-animation",
            "Run Death Animation",
            new KLEPNot(new KLEPKeyPresent("key.health")));

        Expect(!deathLock.Evaluate(keys).IsSatisfied, "Not blocks while Health exists");
        keys.Remove("key.health");
        Expect(deathLock.Evaluate(keys).IsSatisfied, "Not opens after Health is removed");
        keys.Add("key.health");
        Expect(!deathLock.Evaluate(keys).IsSatisfied, "Not recomputes after Health returns");
    }

    private static void VerifyCompositionAndTrace()
    {
        var keys = new FakeKeySource("key.a", "key.c");
        var lockDefinition = MakeLock(new KLEPAll(
            new KLEPKeyPresent("key.a"),
            new KLEPAny(
                new KLEPKeyPresent("key.b"),
                new KLEPKeyPresent("key.c")),
            new KLEPNot(new KLEPKeyPresent("key.dead"))));
        KLEPLockEvaluation evaluation = lockDefinition.Evaluate(keys);

        Expect(evaluation.IsSatisfied, "Nested All/Any/Not composes");
        Expect(evaluation.Results.Count == 7, "Trace contains every leaf and operator");
        Expect(evaluation.Results[evaluation.Results.Count - 1].Path == "root",
            "Trace ends with deterministic root result");
    }

    private static void VerifyEmptyGroupIdentities()
    {
        var keys = new FakeKeySource();
        Expect(MakeLock(new KLEPAll()).Evaluate(keys).IsSatisfied,
            "Empty All is mathematically true and preserves an empty legacy Lock");
        Expect(!MakeLock(new KLEPAny()).Evaluate(keys).IsSatisfied,
            "Empty Any is mathematically false");
    }

    private static void VerifyRepeatability()
    {
        var keys = new FakeKeySource("key.a");
        var lockDefinition = MakeLock(new KLEPAny(
            new KLEPKeyPresent("key.a"),
            new KLEPKeyPresent("key.b")));
        string expected = Serialize(lockDefinition.Evaluate(keys));

        for (int run = 0; run < 100; run++)
        {
            Expect(Serialize(lockDefinition.Evaluate(keys)) == expected,
                $"Repeatability run {run}");
        }
    }

    private static KLEPLock MakeLock(KLEPLockExpression expression)
    {
        return new KLEPLock("lock.test", "Test Lock", expression);
    }

    private static string Serialize(KLEPLockEvaluation evaluation)
    {
        var parts = new List<string>();
        foreach (KLEPLockExpressionResult result in evaluation.Results)
        {
            parts.Add($"{result.Path}|{result.Kind}|{result.StableKeyId}|{result.IsSatisfied}");
        }

        return $"{evaluation.LockId}|{evaluation.IsSatisfied}|{string.Join(";", parts)}";
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {message}");
        }
    }

    private sealed class FakeKeySource : IKLEPLockKeySource
    {
        private readonly HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

        public FakeKeySource(params string[] initialKeys)
        {
            foreach (string key in initialKeys)
            {
                keys.Add(key);
            }
        }

        public bool Contains(string stableKeyId) => keys.Contains(stableKeyId);
        public void Add(string stableKeyId) => keys.Add(stableKeyId);
        public void Remove(string stableKeyId) => keys.Remove(stableKeyId);
    }
}
