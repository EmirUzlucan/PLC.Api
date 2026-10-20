namespace PLC.Api.Models
{
    public class DataBlock
    {
        public int Id { get; set; }

        public int PlcId { get; set; }

        public int DbNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public Plc Plc { get; set; } = null!;

        public ICollection<DataBlockVariable> Variables { get; set; } = new List<DataBlockVariable>();
    }
}
