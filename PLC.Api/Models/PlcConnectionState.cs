namespace PLC.Api.Models
{
    public class PlcConnectionState
    {
        public int PlcId { get; set; }

        public PlcConnectionStatus Status { get; set; }

        public int ReconnectAttemptCount { get; set; }

        public string? LastError { get; set; }
    }

    public enum PlcConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Failed
    }
}
