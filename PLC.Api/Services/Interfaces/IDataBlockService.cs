using PLC.Api.DTOs.DataBlock;
using PLC.Api.Models;

public interface IDataBlockService
{
    (string Name, List<DataBlockVariable> Variables) Parse(Stream file);

    int GetDataTypeSize(string typeDefinition);

    (int Lower, int Upper) GetArrayBounds(string typeDefinition);

    int GetArrayCount(string typeDefinition);

    string GetArrayElementType(string typeDefinition);

    Task<List<VariableChildResponse>> GetVariableChildrenAsync(
        DataBlockVariable variable,
        string path);
}