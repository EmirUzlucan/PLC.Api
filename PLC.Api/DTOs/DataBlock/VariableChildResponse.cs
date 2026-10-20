public class VariableChildResponse
{
    public int? Id { get; set; }

    public int? Index { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool HasChildren { get; set; }
}