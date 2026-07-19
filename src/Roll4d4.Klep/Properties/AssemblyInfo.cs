using System.Runtime.CompilerServices;

// These executable contract suites inspect internal atomicity and ownership
// invariants. Consumers do not receive access to those implementation seams.
[assembly: InternalsVisibleTo("KlepExecutableSmoke")]
[assembly: InternalsVisibleTo("KlepKeySmoke")]
[assembly: InternalsVisibleTo("KlepObserverSmoke")]
