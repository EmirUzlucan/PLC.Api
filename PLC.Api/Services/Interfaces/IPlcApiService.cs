using PLC.Api.DTOs.Plc;

public interface IPlcApiService
{
    Task<List<PlcResponse>> GetAllAsync();
    Task<PlcResponse?> GetByIdAsync(
        int plcId);
    Task<PlcResponse> CreateAsync(
        CreatePlcRequest request);
    Task<PlcResponse?> UpdateAsync(
        int plcId,
        UpdatePlcRequest request);
    Task<bool> DeleteAsync(
        int plcId);
    List<PlcCpuTypeResponse> GetCpuTypes();
    Task<PlcStatusResponse> GetStatusAsync(
        int plcId);
    Task<PlcStatusResponse> ConnectAsync(
        int plcId);
    Task<PlcStatusResponse> DisconnectAsync(
        int plcId);

    // path null ise variable'ın kendi offseti kullanılır.
    // path verilirse ResolveAddress ile gerçek adres hesaplanır.
    Task<VariableValueResponse?> ReadVariableAsync(
        int variableId,
        string? path = null);
    Task<bool> WriteVariableAsync(
        int variableId,
        WriteVariableRequest request,
        string? path = null);
}