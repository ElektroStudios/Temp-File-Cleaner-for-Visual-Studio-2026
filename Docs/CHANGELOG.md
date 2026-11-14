# Temp File Cleaner for Visual Studio 2026 Change Log 📋

## v1.0.1 *(current)* 🆕

### Fixed
- Normalize null log entries to empty strings before asynchronous writing for consistent .NET Framework and Mono behavior.
- Preserve arbitrary 8.3 files and directories instead of treating names such as `document.txt` and `accounts.csv` as Visual Studio temporaries.
- Validate GUID, hexadecimal, and ASCII alphanumeric name components; stop matching punctuation through the `[A-z]` range.
- Stop recursively searching unrelated temporary directories for startup files and compiler DLLs.
- Skip reparse points and their descendants, preserve storage roots, and continue with other entries after filesystem failures.
- Run cleanup during package disposal instead of the cancellable `QueryClose` callback.
- Require a successfully registered, still-present lease before cleanup; prevent repeated cleanup and hold the registration mutex throughout cleanup.
- Fail closed on corrupt lease records and coordination failures, retain processes whose identity cannot be inspected, and replace the registry without truncating the live file first.
- Serialize log writes, avoid capturing the UI synchronization context, and report write failures instead of silently discarding them.
- Remove the unused and incorrectly awaited shell lookup from text service initialization.
- Use the VS2026 log prefix, remove the obsolete `InstalledProductRegistration` attribute, and align assembly/VSIX version metadata and the .NET Framework prerequisite.

### Tests and build
- Add xUnit v3 tests targeting .NET Framework 4.8 for cleanup rules, actual filesystem effects, log output, and instance coordination.

## v1.0
Initial Release.
