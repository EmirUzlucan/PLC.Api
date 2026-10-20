namespace PLC.Api.DTOs.DataBlock
{
    public class DataBlockResponse
    {
        public int Id { get; set; }

        public int PlcId { get; set; }

        public int DbNumber { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
