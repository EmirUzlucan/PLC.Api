using PLC.Api.Models;

public class DataBlockParseResult
{
    public DataBlock DataBlock { get; set; } = null!;

    public List<DataBlockVariable> Variables { get; set; } = new();
}