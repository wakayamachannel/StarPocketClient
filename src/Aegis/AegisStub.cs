// The fallback when AegisService cannot be made (AegisFactory.Create). Behaves like a PC without aegis\Aegis.ps1 today:
// nothing is checked, nothing is blocked, the tray shows Aegis as off.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;

namespace Starpocket.Client.Aegis
{
    internal sealed class AegisStub : IAegisService
    {
        readonly AegisContext context;

        public AegisStub(AegisContext context) { this.context = context; }

        public bool IsPorted => false;
        public event EventHandler Changed { add { } remove { } }

        public void Start() => context?.Log?.Invoke("Aegis: not in this build yet (stub)");
        public void Stop() { }
        public void SetLanguage(string lang) { }
        public bool RefreshDefinitionsIfStale() => false;
        public AegisSnapshot GetSnapshot() => new AegisSnapshot { State = "off" };

        public PreLaunchResult PreLaunchScan(Action<ScanProgress> progress, CancellationToken cancel) => new PreLaunchResult { Ran = false };
        public ScanSummary Rescan(Action<ScanProgress> progress) => new ScanSummary { NotAvailable = true };
        public ScanSummary ScanOnly(Action<ScanProgress> progress) => new ScanSummary { NotAvailable = true };

        public string EventsLogPath => Path.Combine(context?.StateDir ?? "", "events.log");

        public void Dispose() { }
    }
}
