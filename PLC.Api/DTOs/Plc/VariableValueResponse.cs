public class VariableValueResponse
{
    public int VariableId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string TypeDefinition { get; set; } = string.Empty;

    public int ByteOffset { get; set; }

    public byte BitOffset { get; set; }

    public object? Value { get; set; }
}