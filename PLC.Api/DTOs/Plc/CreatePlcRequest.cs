using S7.Net;
using System.ComponentModel.DataAnnotations;
using PLC.Api.Validators;

namespace PLC.Api.DTOs.Plc
{
    public class CreatePlcRequest
    {
        [Required(ErrorMessage = "PLC adı zorunludur.")]
        [StringLength(100, MinimumLength = 3,
            ErrorMessage = "PLC adı 3-100 karakter arasında olmalıdır.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "IP adresi zorunludur.")]
        [IpAddress]
        public string IpAddress { get; set; } = string.Empty;

        public int CpuType { get; set; }

        [Range(0, 7, ErrorMessage = "Rack değeri 0-7 arasında olmalıdır.")]
        public short? Rack { get; set; }

        [Range(0, 31, ErrorMessage = "Slot değeri 0-31 arasında olmalıdır.")]
        public short? Slot { get; set; }

        public bool AutoReconnect { get; set; } = true;

        [Range(1, int.MaxValue,
            ErrorMessage = "Maksimum reconnect denemesi en az 1 olmalıdır.")]
        public int MaxReconnectAttempts { get; set; } = 5;

        [Range(100, int.MaxValue,
            ErrorMessage = "Reconnect aralığı en az 100ms olmalıdır.")]
        public int ReconnectIntervalMs { get; set; } = 3000;
    }
}
