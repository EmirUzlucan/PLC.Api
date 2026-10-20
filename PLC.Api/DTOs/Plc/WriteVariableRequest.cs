using System.ComponentModel.DataAnnotations;

public class WriteVariableRequest
{
    [Required(ErrorMessage = "Değer zorunludur.")]
    public object Value { get; set; } = null!;
}