# Batch Review B1/B2/B4/B5/C0b - Moonshot Kimi K3

## Full Review: Sprint B Remaining (B1/B2/B4/B5) + C0b

**Overall Verdict:** Multiple tasks have **critical/high** defects requiring fixes before merge.  
- **B1 (DatasetSampler lease)** → **Fail** (ownership contract broken)  
- **B2 (Pause semantics)** → **Fail** (test is vacuous)  
- **B4 (Global hotkey)** → **Pass-with-Notes** (resource management risks)  
- **B5 (Recorder resilience/meta)** → **Pass-with-Notes** (fragile detection)  
- **C0b (Capture pipeline)** → **Pass-with-Notes** (thread safety)

---

### B1 – DatasetSamplerService Lease/Resource Management  
**Verdict:** Fail  
**Severity:** Critical (resource leak / ownership ambiguity)  

**Issues & Evidence:**  
1. **Manual snapshots leak** when queue full. `TakeManualSnapshotAsync` disposes the frame **before** handing it to queue → rejected entry leaves resource un-disposed.  
   ```csharp
   using (frame) { await SaveFrameAsync(frame, true, ...); } // disposes before queue!
   ```  
   Loss of consumer-owns semantics (see `DropWrite` comment in `StartAsync`).  
2. **Latest-frame caching makes it worse.** `_latestFrame` is cloned but caller of manual snapshot can consume & dispose it without owning it. Readers holding returned frames double-free risk.  

**Suggested Fix:** Consistently let the queue consumer `RunWriterAsync` dispose; just enqueue cloned frames w/o using-block.

---

### B2 – VideoRecorder Pause Semantics  
**Verdict:** Fail  
**Severity:** High  

**Issues & Evidence:**  
1. **Vacuous test.** `Pause_SkipsFrames_AndResumeContinues` never actually pauses:  
   ```csharp
   await service.StartAsync(...);
   // no PauseAsync() between writes
   frameFiles.Should().HaveCount(4); // asserts ALL written
   ```  
2. Actual implementation **disposes frames while paused** (`frame.Dispose(); continue;`) – correct but untested.  

**Suggested Fix:** Interleave `PauseAsync`/`ResumeAsync` and assert skipped frames are not written (`frameFiles.Count < written`).

---

### B4 – NativeHotKeyApi Message Window & Lifecycle  
**Verdict:** Pass-with-Notes  
**Severity:** Medium  

**Issues & Evidence:**  
1. **Double ownership of the HWND** – destroyed in `RunMessageLoop` *and* in `DisposeAsync`, comments imply only loop owns but code destroys twice. Risk of use-after-destroy race.  
2. **Hung loop tolerated silently.** `Join(3s)` timeout logs nothing, potentially leaves thread running.  
3. `ExecutionContext.SuppressFlow` around thread start unnecessary / noisy.  
4. `Register` can throw if message window init fails – but method advertises `bool` success contract; callers except `TimeoutException` unexpected.  
5. Dispose races: event invoked via `ThreadPool.QueueUserWorkItem` may still fire after disposal begins.  

Overall shape good (bounded id space, `HWND_MESSAGE`, explicit class-name GUID, delegate kept alive).

---

### B5 – VideoRecorder Resilience & Orphaned-Meta Detection  
**Verdict:** Pass-with-Notes  
**Severity:** Medium  

**Issues & Evidence:**  
1. **Provisional flag sniffing via raw `string.Contains`** breaks with any formatting change (`"\u0022provisional\u0022: true"` literal). Should deserialize JSON into anonymous `{ provisional }` DTO.  
2. **Orphan sweep runs inside `StartAsync` and can swallow/recover partially** – if it fails, code logs warning and continues; acceptable but detection is weak.  
3. `WriteMetaAsync` non-provisional uses `Interlocked.Read(ref _framesWritten)` but `RecordFramesAsync` also updates local `frameCount`; mismatch risk when failures occur (frames written vs counted).  
4. Stop error-handling robust; task cancellation & meta final write well-tested (`FrameWriteFailure_StopStillWritesMetaAndClearsState`).  

---

### C0b – Capture→Recorder Pipeline Configuration  
**Verdict:** Pass-with-Notes  
**Severity:** Medium  

**Issues & Evidence:**  
1. **Single `Channel<CapturedFrame>` consumed by recorder + sampler**: competing readers starve each other; sampling/manual snapshot may contend with recorder.  
2. `MaxFrameCallbackMilliseconds` uses `Interlocked.Read(ref long)` – not guaranteed atomic on 32-bit; use `Volatile.Read`/explicit lock.  
3. Throttle logic (`ShouldAccept`) only in capture side; when queue rejects frames, `DropWrite` comment indicates caller disposes rejected — aligned with sampler **only for auto mode**, manual snapshot diverges (see B1).  
4. `OnCaptureEnded` fires when provider self-terminates, then dashboard hotkey handler can still call `TakeManualSnapshotAsync` leading to `InvalidOperationException` being silently swallowed – OK but semantics unclear.  

---

## Final Recommendation  
**Fail overall** until B1/B2 are corrected and B4/B5/C0b notes addressed (especially queue ownership + pause tests). Suggest follow-up commits before merge.