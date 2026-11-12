namespace kms.Services.KeyCustody;

/// <summary>Settings for the key custody processor ("KeyCustody" section in appsettings.json).</summary>
public sealed class KeyCustodyOptions
{
    /// <summary>Turn the background processor on/off.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often new scans are checked (seconds).</summary>
    public int PollSeconds { get; set; } = 10;

    /// <summary>A face scan up to this many seconds BEFORE or AFTER a key scan is paired with it.
    /// In the real data ~80% of face scans are within 15 s of the key scan.</summary>
    public int PairWindowSeconds { get; set; } = 30;

    /// <summary>The same key scanned again within this many seconds is treated as a double tap.</summary>
    public int DoubleScanSeconds { get; set; } = 60;

    /// <summary>For a key scanned without a face: people whose face was scanned within this
    /// many seconds are listed as "at the board".</summary>
    public int PresenceWindowSeconds { get; set; } = 180;

    /// <summary>Today's end-of-day checks run after this hour (keys still out, silent keys, ...).</summary>
    public int SettleHour { get; set; } = 20;

    /// <summary>A key still out after this hour is shown as overdue on the live board.</summary>
    public int OverdueHour { get; set; } = 18;

    /// <summary>Create "silent key" incidents (authorized holders at the board, key never scanned).</summary>
    public bool DetectSilentKeys { get; set; } = true;
}
