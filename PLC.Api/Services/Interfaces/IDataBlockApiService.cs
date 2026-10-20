using PLC.Api.DTOs.DataBlock;

public interface IDataBlockApiService
{
    Task<List<DataBlockResponse>> GetAllAsync();

    Task<List<DataBlockResponse>> GetByPlcIdAsync(int plcId);

    Task<DataBlockResponse?> GetByIdAsync(int dbId);

    Task<DataBlockDetailResponse> CreateAsync(CreateDataBlockRequest request);

    Task<DataBlockDetailResponse?> UpdateAsync(int dbId, UpdateDataBlockRequest request);

    Task<bool> DeleteAsync(int dbId);

    Task<List<DataBlockVariableResponse>> GetVariablesAsync(int dbId);

    Task<DataBlockVariableResponse?> GetVariableAsync(int dbId, int variableId);

    Task<List<VariableChildResponse>> GetRootVariablesAsync(int dbId);

    Task<List<VariableChildResponse>> GetVariableChildrenAsync(int dbId, int variableId, string path);
}