using System;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport.Transports;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
  /// <summary>
  /// Supervises the Unity MCP stdio bridge so a session is available without starting
  /// it by hand from Window > MCP for Unity each launch.
  ///
  /// This project's Claude tooling standardizes on the STDIO transport (see
  /// Documentation/Unity-MCP-Setup.md and CLAUDE.md), but the transport pref is a
  /// machine-global EditorPref that keeps getting WIPED: any Unity-family process
  /// (an upgrade's first boot, a batchmode build) that exits after an external
  /// `defaults write` flushes its own prefs snapshot over it, and the package then
  /// treats "unset" as HTTP and auto-starts the wrong bridge.
  ///
  /// Self-heal, scoped to THIS project: whenever stdio is observed selected, that
  /// fact is recorded in the project's own EditorUserSettings (UserSettings/, not
  /// committed, not machine-global). A later UNSET pref is healed back to stdio
  /// only when that record exists — so a wiped machine-global key is restored for
  /// a project known to run stdio, while a fresh clone/first boot (no record) and
  /// other projects' defaults are left alone. The heal writes from inside the
  /// editor (EditorPrefs writes survive Unity's snapshot flush, unlike external
  /// `defaults write`). An EXPLICIT true — somebody chose HTTP in the MCP window —
  /// is respected, loudly, and clears the record.
  ///
  /// The package's StdioBridgeHost auto-starts in the stdio case too, but that path
  /// is gated on a cached pref read whose value depends on [InitializeOnLoad]
  /// ordering and so is unreliable on a cold open. This reads the pref directly and
  /// explicitly kicks the start once the editor is idle, covering every init
  /// ordering. Start is idempotent (StartAutoConnect stops first).
  ///
  /// Unlike a fire-once starter, this stays subscribed to EditorApplication.update
  /// for the lifetime of the domain and acts as a supervisor: if the bridge session
  /// drops mid-run (without a domain reload to re-run [InitializeOnLoad]), it is
  /// noticed and restarted. Attempts are bounded per outage so a permanently
  /// unavailable port (e.g. held by another editor) can't spam the console forever;
  /// the budget resets every time the bridge is observed running, and a manual
  /// Start Session is picked up automatically because the running state is polled.
  /// </summary>
  [InitializeOnLoad]
  internal static class UnityMcpStdioAutoStart
  {
    // Mirrors EditorPrefKeys.UseHttpTransport (internal in the package). Default
    // true => HTTP, matching the package's own default.
    private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";

    // Per-project record (EditorUserSettings) that stdio was deliberately in use
    // here — the scope guard that keeps the heal from deciding for other projects.
    private const string StdioPreferredConfigKey = "UnityMcpStdioAutoStart.StdioPreferred";

    // Cadence for (re)connect attempts while the bridge is down.
    private const double RetryIntervalSeconds = 3d;
    // Slower cadence for the running health check; keeps per-frame work trivial.
    private const double HealthCheckIntervalSeconds = 5d;
    // Bounded attempts per outage so a permanently-unavailable port can't spam the
    // console forever. Reset whenever the bridge is observed running, so a session
    // that drops mid-run gets a fresh budget.
    private const int MaxAttemptsPerOutage = 20;

    // Compiling/updating passes: re-check soon rather than waiting a full interval.
    private const double BusyRecheckSeconds = 0.5d;

    // Next time Tick should do real work. Gating on this at the very top makes the
    // per-frame cost a single timeSinceStartup read + compare; everything else runs
    // at most every RetryIntervalSeconds/HealthCheckIntervalSeconds.
    private static double _nextRun;
    private static int _attemptCount;
    private static bool _wasRunning;
    private static bool _gaveUp;
    // Set when the machine-global pref was healed from the per-project record: the
    // package's configuration cache may have latched the stale HTTP default during
    // [InitializeOnLoad], so converge it once the editor is idle (in Tick).
    private static bool _cacheSyncPending;

    static UnityMcpStdioAutoStart()
    {
      // Don't interfere with headless/CI automation runs; StdioBridgeHost has its
      // own batch-mode handling gated on UNITY_MCP_ALLOW_BATCH.
      if (Application.isBatchMode)
      {
        return;
      }

      if (EditorPrefs.HasKey(UseHttpTransportKey))
      {
        if (EditorPrefs.GetBool(UseHttpTransportKey, true))
        {
          // Explicit HTTP choice: respect it and forget the stdio record so the heal
          // cannot fight a deliberate transport switch after the next wipe.
          EditorUserSettings.SetConfigValue(StdioPreferredConfigKey, null);
          // Never exit silently: the silent-exit was itself a documented diagnosis trap.
          Debug.Log(
            "[UnityMcpStdioAutoStart] HTTP transport is explicitly selected — leaving it alone " +
            "(stdio auto-start disabled; flip it in Window > MCP for Unity if this is unintended).");
          return;
        }

        // stdio is selected: remember that for this project, so a future wipe of the
        // machine-global pref can be healed with evidence instead of a blind default.
        EditorUserSettings.SetConfigValue(StdioPreferredConfigKey, "true");
      }
      else if (EditorUserSettings.GetConfigValue(StdioPreferredConfigKey) == "true")
      {
        // The machine-global pref is gone but this project is on record as running
        // stdio: heal it. Only the lightweight EditorPrefs write happens here — the
        // package warns its services may not be initialized during [InitializeOnLoad];
        // the configuration cache is converged later in Tick.
        EditorPrefs.SetBool(UseHttpTransportKey, false);
        _cacheSyncPending = true;
        Debug.Log(
          "[UnityMcpStdioAutoStart] UseHttpTransport pref was wiped (an upgrade or a prefs-snapshot " +
          "flush) — restored stdio per this project's recorded preference and starting the bridge.");
      }
      else
      {
        // Unset and no record: a first boot on this machine/project, not a wipe. Don't
        // decide a machine-global default from here — point at the one-time setup instead.
        Debug.Log(
          "[UnityMcpStdioAutoStart] UseHttpTransport pref is unset and this project has no recorded " +
          "stdio preference (first boot?) — leaving the package default. Select the stdio transport " +
          "once in Window > MCP for Unity to enable auto-start and wipe self-healing.");
        return;
      }

      _nextRun = EditorApplication.timeSinceStartup + 2d;
      _attemptCount = 0;
      _wasRunning = false;
      _gaveUp = false;
      EditorApplication.update += Tick;
    }

    private static void Tick()
    {
      // Throttle gate: the only work done on most frames is this read + compare.
      var now = EditorApplication.timeSinceStartup;
      if (now < _nextRun)
      {
        return;
      }

      if (EditorApplication.isCompiling || EditorApplication.isUpdating)
      {
        _nextRun = now + BusyRecheckSeconds;
        return;
      }

      if (_cacheSyncPending)
      {
        // Converge the package's configuration cache with the healed pref: if a package
        // [InitializeOnLoad] ctor initialized the cache to the HTTP default before our heal
        // ran, its delayCall HTTP auto-start would read the stale value. Safe here (editor
        // idle, services up), a no-op when the cache already read the healed pref, and the
        // pending flag only clears on SUCCESS so a transient throw is retried next tick.
        try
        {
          EditorConfigurationCache.Instance.SetUseHttpTransport(false);
          _cacheSyncPending = false;
        }
        catch (Exception e)
        {
          if (_attemptCount == 0)
          {
            Debug.LogWarning(
              $"[UnityMcpStdioAutoStart] Could not sync the MCP configuration cache yet (will retry): {e.Message}");
          }
        }
      }

      if (StdioBridgeHost.IsRunning)
      {
        // Healthy. Note the transition, clear any failure budget, and keep polling
        // (slowly) so a later drop is noticed. We intentionally stay subscribed —
        // this is what makes it a supervisor rather than a one-shot starter.
        if (!_wasRunning)
        {
          Debug.Log("[UnityMcpStdioAutoStart] Stdio MCP bridge is running.");
        }
        _wasRunning = true;
        _attemptCount = 0;
        _gaveUp = false;
        _nextRun = now + HealthCheckIntervalSeconds;
        return;
      }

      // Bridge is down. If it was up a moment ago, the session dropped mid-run — log
      // once and resume attempts with a fresh budget (this pass falls through to an
      // immediate attempt below).
      if (_wasRunning)
      {
        Debug.Log("[UnityMcpStdioAutoStart] Stdio MCP bridge session dropped; attempting to restart.");
        _wasRunning = false;
        _gaveUp = false;
        _attemptCount = 0;
      }

      // Exhausted this outage's budget: stop attempting to avoid console spam, but
      // keep polling slowly so a manual Start Session (or any later running state) is
      // still picked up by the healthy branch above and re-arms the supervisor.
      if (_gaveUp)
      {
        _nextRun = now + HealthCheckIntervalSeconds;
        return;
      }

      _attemptCount++;
      _nextRun = now + RetryIntervalSeconds;

      try
      {
        StdioBridgeHost.StartAutoConnect();
        if (StdioBridgeHost.IsRunning)
        {
          Debug.Log($"[UnityMcpStdioAutoStart] Stdio MCP bridge started on attempt {_attemptCount}.");
          _wasRunning = true;
          _attemptCount = 0;
          _nextRun = now + HealthCheckIntervalSeconds;
        }
      }
      catch (Exception e)
      {
        // Retry a bounded number of times, then give up quietly so a persistent
        // failure (e.g. port held by another editor) never spams the console.
        if (_attemptCount == 1 || _attemptCount % 5 == 0)
        {
          Debug.LogWarning($"[UnityMcpStdioAutoStart] Bridge start attempt {_attemptCount} failed: {e.Message}");
        }

        if (_attemptCount >= MaxAttemptsPerOutage)
        {
          Debug.LogWarning(
            $"[UnityMcpStdioAutoStart] Giving up after {_attemptCount} attempts; will resume if the bridge is started manually.");
          _gaveUp = true;
        }
      }
    }
  }
}
