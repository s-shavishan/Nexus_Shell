// නමෝ බුද්ධාය 🤍
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal sealed class ClipboardCaptureService : IDisposable
{
    private const uint SubclassId = 0x4E56, UnicodeText = 13;
    private readonly IntPtr _window;
    private readonly ShellLayerInterop.SubclassProc _callback;
    private readonly Func<Action, bool> _dispatch;
    private readonly ClipboardHistory _history;
    private readonly ClipboardCaptureGate _gate = new();
    private readonly uint _exclude, _historyFormat, _cloudFormat;
    private bool _hooked, _sessionEvents, _listening, _disconnected;
    private volatile bool _locked, _disposed;
    private int _writing;
    private uint _baseline;
    internal event Action? Changed;
    internal event Action<bool>? SessionAvailabilityChanged;
    internal string Status { get; private set; } = "Off · nothing is retained.";
    internal bool Suspended => _locked || _disconnected;
    internal bool Available => _hooked && _sessionEvents;
    internal ClipboardCaptureService(IntPtr window, ClipboardHistory history, Func<Action, bool> dispatch)
    {
        _window = window; _history = history; _dispatch = dispatch; _callback = Message;
        _exclude = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        _historyFormat = RegisterClipboardFormat("CanIncludeInClipboardHistory"); _cloudFormat = RegisterClipboardFormat("CanUploadToCloudClipboard");
        _hooked = ShellLayerInterop.SetWindowSubclass(window, _callback, new UIntPtr(SubclassId), UIntPtr.Zero);
        if (_hooked) _sessionEvents = WTSRegisterSessionNotification(window, 0);
        if (!Available || _exclude == 0 || _historyFormat == 0 || _cloudFormat == 0) Status = "Unavailable · Windows session or clipboard notifications could not attach.";
    }
    internal bool SetEnabled(bool enabled)
    {
        if (_disposed) return false;
        if (_listening == enabled) return !enabled || Available;
        if (enabled && (!Available || _exclude == 0 || _historyFormat == 0 || _cloudFormat == 0 || !AddClipboardFormatListener(_window)))
        { Status = "Unavailable · clipboard capture stays off because Windows notifications could not attach."; Changed?.Invoke(); return false; }
        if (!enabled && _listening) RemoveClipboardFormatListener(_window);
        _listening = enabled; _baseline = GetClipboardSequenceNumber(); _gate.Invalidate(); _history.SetEnabled(enabled);
        Status = !enabled ? "Off · nothing is retained." : Suspended ? "Paused while this session is locked or disconnected." : "On · new copied text stays in this session.";
        Changed?.Invoke(); return true;
    }
    internal void Clear()
    { _gate.Invalidate(); _baseline = GetClipboardSequenceNumber(); _history.Clear(); if (_listening) Status = Suspended ? "Paused while this session is locked or disconnected." : _gate.Busy ? "Waiting for the current clipboard source; cleared entries will not return." : "On · new copied text stays in this session."; Changed?.Invoke(); }
    internal void Suspend()
    { _locked = true; Clear(); if (_listening) Status = "Paused while this session is locked or disconnected."; Changed?.Invoke(); }
    private IntPtr Message(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr data)
    {
        try
        {
            if (!_disposed && message == 0x031D) ReadChanged(); // WM_CLIPBOARDUPDATE
            else if (!_disposed && message == 0x02B1) // WM_WTSSESSION_CHANGE
            {
                uint action = unchecked((uint)wp.ToUInt64());
                if (action is 2 or 4) { _disconnected = true; Clear(); Changed?.Invoke(); }
                else if (action is 6 or 7) Suspend();
                else if (action is 1 or 3 or 5 or 8)
                {
                    if (action == 8) _locked = false;
                    if (action is 1 or 3) _disconnected = false;
                    _baseline = GetClipboardSequenceNumber();
                    if (_listening) Status = Suspended ? "Paused while this session is locked or disconnected." : "On · new copied text stays in this session.";
                    Changed?.Invoke();
                }
                if (action is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8) SessionAvailabilityChanged?.Invoke(Suspended);
            }
        }
        catch { Status = "Clipboard notifications paused. Turn the shelf off and on to retry."; Changed?.Invoke(); }
        return ShellLayerInterop.DefSubclassProc(window, message, wp, lp);
    }
    private void ReadChanged()
    {
        if (_disposed || !_listening || Suspended) return; uint sequence = GetClipboardSequenceNumber();
        if (sequence == 0 || sequence == _baseline) return;
        var lease = _gate.Begin(sequence); if (lease is null) return;
        _baseline = sequence; _ = ReadAsync(lease.Value);
    }
    private async Task ReadAsync(ClipboardCaptureGate.Lease lease)
    {
        var read = Task.Run(() => ReadText(lease.Sequence));
        var winner = await Task.WhenAny(read, Task.Delay(3000)).ConfigureAwait(false);
        if (winner != read) _dispatch(() =>
        {
            if (_disposed || !_gate.Current(lease) || !_listening || Suspended) return;
            Status = "A clipboard source is slow. Capture waits for it; the desktop stays available."; Changed?.Invoke();
        });
        string? text = null;
        try { text = await read.ConfigureAwait(false); } catch { /* Contents are never logged. */ }
        finally { _gate.End(lease); }
        _dispatch(() =>
        {
            if (_disposed || !_listening || Suspended) return;
            Status = "On · new copied text stays in this session.";
            if (_gate.Current(lease) && GetClipboardSequenceNumber() == lease.Sequence) _history.Add(text, DateTimeOffset.Now);
            Changed?.Invoke();
            if (GetClipboardSequenceNumber() != _baseline) ReadChanged();
        });
    }
    private string? ReadText(uint sequence)
    {
        if (GetClipboardSequenceNumber() != sequence || !IsClipboardFormatAvailable(UnicodeText)) return null;
        bool opened = false;
        for (int i = 0; i < 3 && !opened; i++) { opened = OpenClipboard(IntPtr.Zero); if (!opened) Thread.Sleep(20); }
        if (!opened) return null;
        try
        {
            if (GetClipboardSequenceNumber() != sequence || !ClipboardCapturePolicy.Allows(IsClipboardFormatAvailable(_exclude), false, null)) return null;
            if (IsClipboardFormatAvailable(_historyFormat))
            {
                IntPtr marker = GetClipboardData(_historyFormat); if (marker == IntPtr.Zero || GlobalSize(marker).ToUInt64() < 4) return null;
                IntPtr pointer = GlobalLock(marker); if (pointer == IntPtr.Zero) return null;
                try { if (!ClipboardCapturePolicy.Allows(false, true, Marshal.ReadInt32(pointer))) return null; } finally { GlobalUnlock(marker); }
            }
            IntPtr handle = GetClipboardData(UnicodeText); if (handle == IntPtr.Zero) return null;
            ulong bytes = GlobalSize(handle).ToUInt64();
            if (!ClipboardCapturePolicy.PayloadSize(bytes)) return null;
            IntPtr memory = GlobalLock(handle); if (memory == IntPtr.Zero) return null;
            try
            {
                int maximum = Math.Min((int)(bytes / 2), ClipboardHistory.MaximumCharacters + 1), length = 0;
                while (length < maximum && Marshal.ReadInt16(memory, length * 2) != 0) length++;
                if (length == maximum || length > ClipboardHistory.MaximumCharacters || GetClipboardSequenceNumber() != sequence) return null;
                return Marshal.PtrToStringUni(memory, length);
            }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }
    internal bool Copy(string text)
    {
        if (Interlocked.Exchange(ref _writing, 1) != 0) return false;
        try { return CopyText(text); } finally { Volatile.Write(ref _writing, 0); }
    }
    private bool CopyText(string text)
    {
        if (_disposed || Suspended || string.IsNullOrEmpty(text) || text.Length > ClipboardHistory.MaximumCharacters || text.Contains('\0')) return false;
        IntPtr content = GlobalAlloc(2, new UIntPtr((uint)((text.Length + 1) * 2))), marker = GlobalAlloc(2, new UIntPtr(4));
        if (content == IntPtr.Zero || marker == IntPtr.Zero) { if (content != IntPtr.Zero) GlobalFree(content); if (marker != IntPtr.Zero) GlobalFree(marker); return false; }
        bool opened = false;
        try
        {
            IntPtr data = GlobalLock(content); if (data == IntPtr.Zero) return false;
            try { Marshal.Copy((text + "\0").ToCharArray(), 0, data, text.Length + 1); } finally { GlobalUnlock(content); }
            IntPtr flag = GlobalLock(marker); if (flag == IntPtr.Zero) return false;
            try { Marshal.WriteInt32(flag, 0); } finally { GlobalUnlock(marker); }
            opened = OpenClipboard(_window); if (!opened || !EmptyClipboard()) return false;
            // This presence marker excludes replayed shelf text from Windows
            // history/cloud processing too. Set it before publishing the text.
            if (SetClipboardData(_exclude, marker) == IntPtr.Zero) return false; marker = IntPtr.Zero;
            if (SetClipboardData(UnicodeText, content) == IntPtr.Zero) return false; content = IntPtr.Zero;
            _baseline = GetClipboardSequenceNumber(); _gate.Invalidate(); return true;
        }
        finally { if (opened) CloseClipboard(); if (content != IntPtr.Zero) GlobalFree(content); if (marker != IntPtr.Zero) GlobalFree(marker); }
    }
    public void Dispose()
    {
        if (_disposed) return; SetEnabled(false); _disposed = true; _gate.Invalidate(); _history.Clear(); Changed = null; SessionAvailabilityChanged = null;
        if (_sessionEvents) WTSUnRegisterSessionNotification(_window);
        if (_hooked) ShellLayerInterop.RemoveWindowSubclass(_window, _callback, new UIntPtr(SubclassId));
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr handle);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSRegisterSessionNotification(IntPtr window, uint flags);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSUnRegisterSessionNotification(IntPtr window);
}
