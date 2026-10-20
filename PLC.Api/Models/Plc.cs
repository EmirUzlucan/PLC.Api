using S7.Net;

namespace PLC.Api.Models
{
    public class Plc
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;

        public CpuType CpuType { get; set; }

        public short Rack { get; set; }

        public short Slot { get; set; }

        public bool AutoReconnect { get; set; } = true;

        public int MaxReconnectAttempts { get; set; } = 5;

        public int ReconnectIntervalMs { get; set; } = 3000;

        public ICollection<DataBlock> DataBlocks { get; set; } = new List<DataBlock>();
    }
}
