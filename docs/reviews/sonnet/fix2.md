Verdict: Pass-with-Notes

**Critical**

None found.

**Major**

- `src/ChanSight.Vision/Services/OpenRouterVlmClient.cs:70-77` — `HttpRequestException` and `TaskCanceledException` are wrapped in `VlmUnavailableException`, but the constructor call sites don't pass `innerException` consistently for the timeout branch — the `TaskCanceledException` case discards the original exception entirely (no `ex` passed as inner). This loses diagnostic context (stack trace, actual timeout cause vs. user cancellation) that would help distinguish network timeout from other `TaskCanceledException` sources. Consider `new VlmUnavailableException("...", ex)`.
- `src/ChanSight.Vision/Services/OpenRouterVlmClient.cs` — Comment claims "single retry" lives in `VlmRecognitionAdapter`, but this file has zero visibility into that invariant; if the adapter's retry logic changes independently, this comment silently rots. Consider a shared constant or XML-doc cross-reference test (which does exist in `VlmRecognitionAdapterTests.cs`) — good that it's covered, but note the coupling is implicit/documentation-only, not enforced by any interface contract.

**Minor**

- `src/ChanSight.Vision/Services/VlmUnavailableException.cs:9-11` — Added parameterless constructor is unused by any call site; if intentional for serialization/general exception convention, fine, but consider `[ExcludeFromCodeCoverage]` or a comment explaining why it's needed (typical exception-design guideline compliance) since none of the three current call sites use it.
- `tests/ChanSight.Tests/Vision/OpenRouterVlmClientTests.cs:71` — Missing newline at end of file.
- `tests/ChanSight.Tests/Recorder/ReplayLoaderTests.cs` new test — good coverage for R1 null-guard, but no corresponding assertion on `result.Events.Count` (only `.Select(...).Equal`) — fine, but consider adding count check for extra clarity against off-by-one bugs in skip logic.
- `GameStateManagerTests.cs:87-98` — Good E2 ownership test; consider also asserting that `manager.Update` with a fresh `Version = 0` (default struct value) still increments correctly, to guard against an off-by-one regression if the "ignore incoming version" logic is later refactored to a conditional check.

Overall: the three targeted fixes (vendor-prefixed model id, single-attempt client with adapter-owned retry, version ownership in `GameStateManager`) are correctly implemented and well-tested with clear regression tests pinning the "total requests capped at 2" and "version always owned by state machine" invariants.
