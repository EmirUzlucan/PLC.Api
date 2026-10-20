using S7.Net;

namespace PLC.Api.DTOs.Plc
{
    public class PlcResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;

        public int CpuType { get; set; }

        public short Rack { get; set; }

        public short Slot { get; set; }

        public bool AutoReconnect { get; set; }

        public int MaxReconnectAttempts { get; set; }

        public int ReconnectIntervalMs { get; set; }
    }
}
