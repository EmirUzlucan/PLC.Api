using PLC.Api.Models;

public interface IPlcService
{
    Task ConnectAsync(Plc plc);

    void Disconnect(int plcId);

    Task<bool> IsConnectedAsync(int plcId);

    PlcConnectionState GetConnectionState(int plcId);

    // Path'e göre gerçek byte/bit offsetini hesaplar
    ResolvedAddress ResolveAddress(
        DataBlockVariable variable,
        string path);

    

    // Hesaplanmış gerçek adresten okuma
    object? ReadValue(
        Plc plc,
        DataBlock dataBlock,
        string typeDefinition,
        int byteOffset,
        int bitOffset);

    // Hesaplanmış gerçek adrese yazma
    void WriteValue(
        Plc plc,
        DataBlock dataBlock,
        string typeDefinition,
        int byteOffset,
        int bitOffset,
        object value);

    // Variable'ın kendi kayıtlı offsetinden okuma
    object? ReadVariable(
        Plc plc,
        DataBlock dataBlock,
        DataBlockVariable variable);

    // Variable'ın kendi kayıtlı offsetine yazma
    void WriteVariable(
        Plc plc,
        DataBlock dataBlock,
        DataBlockVariable variable,
        object value);
}