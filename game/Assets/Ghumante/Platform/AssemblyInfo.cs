using System.Runtime.CompilerServices;

// EditMode tests drive GatedHaptics on a fake clock (PlayAt, PlayDeferredIfDue) instead of waiting for frames.
[assembly: InternalsVisibleTo("Ghumante.Tests.EditMode")]
