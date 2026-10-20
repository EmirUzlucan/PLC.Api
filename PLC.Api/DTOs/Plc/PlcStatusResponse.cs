using PLC.Api.Models;

namespace PLC.Api.DTOs.Plc
{
    public class PlcStatusResponse
    {
        public int PlcId { get; set; }

        public string Status { get; set; } = string.Empty;

        public int ReconnectAttemptCount { get; set; }

        public string? LastError { get; set; }
    }
}
