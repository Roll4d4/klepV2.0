# Minimal console consumer

This example is a real consumer of `Roll4d4.Klep`; it does not use test-only
helpers or conceptual placeholder variables. A host supplies one boolean world
observation before each Tick:

- a Tandem Sensor converts `humanInRange` into a one-cycle Key;
- an `Eat human` Goal is eligible only while that Key is present;
- a lower-scored `Wander` Action remains the fallback;
- the selected behavior emits an intent, which the host reads from the frozen
  Tick trace and applies only after the Tick completes.

Run it from the repository root:

```powershell
dotnet run --project examples/KlepMinimalConsole/KlepMinimalConsole.csproj
```

The program runs the same three observations twice and exits with an error if
the immutable trace signatures differ. Its console output also shows Tandem
waves, the frozen Key set, eligibility, scores, selection, lifecycle results,
and the post-Tick host effect boundary.
