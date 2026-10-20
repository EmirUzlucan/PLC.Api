using PLC.Api.Models;

public class DataBlockVariable
{
    public int Id { get; set; }

    public int DataBlockId { get; set; }
    public DataBlock DataBlock { get; set; } = null!;

    public int? ParentId { get; set; }
    public DataBlockVariable? Parent { get; set; }

    public List<DataBlockVariable> Children { get; set; } = new();

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string TypeDefinition { get; set; } = string.Empty;

    public int ByteOffset { get; set; }

    public byte BitOffset { get; set; }

    public int Size { get; set; }
}