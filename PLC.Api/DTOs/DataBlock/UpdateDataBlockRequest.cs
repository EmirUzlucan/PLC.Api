using System.ComponentModel.DataAnnotations;

namespace PLC.Api.DTOs.DataBlock
{
    public class UpdateDataBlockRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "PLC ID geçerli bir sayı olmalıdır.")]
        public int? PlcId { get; set; }

        [Range(0, 65535, ErrorMessage = "DataBlock numarası 0-65535 arasında olmalıdır.")]
        public int? DbNumber { get; set; }

        public IFormFile? File { get; set; }
    }
}
