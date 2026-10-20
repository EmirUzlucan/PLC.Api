using Microsoft.EntityFrameworkCore.Query;
using PLC.Api.Models;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using DataType = S7.Net.DataType;
using S7NetPlc = S7.Net.Plc;
using VarType = S7.Net.VarType;

namespace PLC.Api.Services
{
    public class PlcService : IPlcService
    {
        private readonly IDataBlockService _dataBlockService;

        public PlcService(IDataBlockService dataBlockService)
        {
            _dataBlockService = dataBlockService;
        }

        private readonly Dictionary<int, S7NetPlc> _connections = new();
        private readonly Dictionary<int, CancellationTokenSource> _reconnectTokens = new();
        private readonly Dictionary<int, CancellationTokenSource> _monitorTokens = new();
        private readonly Dictionary<int, PlcConnectionState> _states = new();

        private readonly object _lock = new();

        #region Public Connection API

        public async Task ConnectAsync(Plc plc)
        {
            StopReconnect(plc.Id);
            StopConnectionMonitor(plc.Id);
            RemoveConnection(plc.Id);

            SetStatus(plc.Id, PlcConnectionStatus.Connecting);

            var connection = new S7NetPlc(
                plc.CpuType,
                plc.IpAddress,
                plc.Rack,
                plc.Slot);

            try
            {
                await Task.Run(() => connection.Open());

                if (!connection.IsConnected)
                    throw new InvalidOperationException(
                        $"{plc.Name} bağlantısı kurulamadı.");

                lock (_lock)
                {
                    _connections[plc.Id] = connection;
                }

                SetStatus(
                    plc.Id,
                    PlcConnectionStatus.Connected);

                ResetReconnectState(plc.Id);

                StartConnectionMonitor(plc);
            }
            catch
            {
                connection.Close();

                SetStatus(
                    plc.Id,
                    PlcConnectionStatus.Disconnected);

                throw;
            }
        }

        public void Disconnect(int plcId)
        {
            StopReconnect(plcId);
            StopConnectionMonitor(plcId);

            RemoveConnection(plcId);

            SetStatus(
                plcId,
                PlcConnectionStatus.Disconnected);
        }

        public async Task<bool> IsConnectedAsync(int plcId)
        {
            S7NetPlc? plc;

            lock (_lock)
            {
                if (!_connections.TryGetValue(plcId, out plc))
                    return false;
            }

            if (!plc.IsConnected)
            {
                RemoveConnection(plcId);
                SetStatus(plcId, PlcConnectionStatus.Disconnected);
                return false;
            }

            if (!await PingHostAsync(plc.IP))
            {
                RemoveConnection(plcId);
                SetStatus(plcId, PlcConnectionStatus.Disconnected);
                return false;
            }

            if (!await CheckTcpPortAsync(plc.IP, 102))
            {
                RemoveConnection(plcId);
                SetStatus(plcId, PlcConnectionStatus.Disconnected);
                return false;
            }

            return true;
        }

        #endregion

        #region Status Management

        private PlcConnectionState GetOrCreateStateUnsafe(int plcId)
        {
            if (!_states.TryGetValue(plcId, out var state))
            {
                state = new PlcConnectionState
                {
                    PlcId = plcId,
                    Status = PlcConnectionStatus.Disconnected
                };

                _states[plcId] = state;
            }

            return state;
        }

        private void SetStatus(int plcId, PlcConnectionStatus status)
        {
            lock (_lock)
            {
                var state = GetOrCreateStateUnsafe(plcId);
                state.Status = status;
            }
        }

        public PlcConnectionState GetConnectionState(int plcId)
        {
            lock (_lock)
            {
                var state = GetOrCreateStateUnsafe(plcId);

                return new PlcConnectionState
                {
                    PlcId = state.PlcId,
                    Status = state.Status,
                    ReconnectAttemptCount = state.ReconnectAttemptCount,
                    LastError = state.LastError
                };
            }
        }

        #endregion

        #region Connection Management

        private S7NetPlc GetConnection(int plcId)
        {
            S7NetPlc connection;

            lock (_lock)
            {
                if (!_connections.TryGetValue(plcId, out connection!))
                    throw new InvalidOperationException("PLC bağlı değil.");
            }

            if (!connection.IsConnected)
            {
                RemoveConnection(plcId);
                SetStatus(plcId, PlcConnectionStatus.Disconnected);
                throw new InvalidOperationException("PLC bağlantısı kesilmiş.");
            }

            return connection;
        }

        private void RemoveConnection(int plcId)
        {
            S7NetPlc? connection = null;

            lock (_lock)
            {
                if (_connections.TryGetValue(plcId, out connection))
                    _connections.Remove(plcId);
            }

            connection?.Close();
        }

        #endregion

        #region Connection Monitoring

        private void StartConnectionMonitor(
            Plc plc,
            int intervalMs = 3000)
        {
            StopConnectionMonitor(plc.Id);

            if (intervalMs <= 0)
                intervalMs = 3000;

            CancellationTokenSource cts = new();

            lock (_lock)
            {
                _monitorTokens[plc.Id] = cts;
            }

            _ = Task.Run(() =>
                ConnectionMonitorLoopAsync(
                    plc,
                    intervalMs,
                    cts.Token));
        }

        private async Task ConnectionMonitorLoopAsync(
            Plc plc,
            int intervalMs,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        intervalMs,
                        cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    bool connected =
                        await IsConnectedAsync(plc.Id);

                    if (connected)
                        continue;

                    // Bağlantı kopmuş durumda.
                    StopConnectionMonitor(plc.Id);

                    if (plc.AutoReconnect)
                    {
                        StartReconnect(plc);
                    }

                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Monitor kendi başına uygulamayı düşürmesin.
                }
            }
        }

        private void StopConnectionMonitor(int plcId)
        {
            CancellationTokenSource? cts = null;

            lock (_lock)
            {
                if (_monitorTokens.TryGetValue(
                        plcId,
                        out cts))
                {
                    _monitorTokens.Remove(plcId);
                }
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        #endregion

        #region Network Validation

        private async Task<bool> PingHostAsync(string ipAddress, int timeout = 1000)
        {
            try
            {
                using Ping ping = new();
                PingReply reply = await ping.SendPingAsync(ipAddress, timeout);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckTcpPortAsync(string ipAddress, int port, int timeout = 1000)
        {
            try
            {
                using TcpClient client = new();

                Task connectTask = client.ConnectAsync(ipAddress, port);
                Task timeoutTask = Task.Delay(timeout);
                Task completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                    return false;

                if (connectTask.IsFaulted)
                    return false;

                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Reconnect Logic

        private void StartReconnect(Plc plc)
        {
            StopReconnect(plc.Id);

            CancellationTokenSource cts = new();

            lock (_lock)
            {
                _reconnectTokens[plc.Id] = cts;
            }

            _ = Task.Run(() => ReconnectLoopAsync(plc, cts.Token));
        }

        private async Task ReconnectLoopAsync(
            Plc plc,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        plc.ReconnectIntervalMs,
                        cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    lock (_lock)
                    {
                        var state =
                            GetOrCreateStateUnsafe(plc.Id);

                        if (state.ReconnectAttemptCount >=
                            plc.MaxReconnectAttempts)
                        {
                            state.Status =
                                PlcConnectionStatus.Failed;

                            return;
                        }

                        state.Status =
                            PlcConnectionStatus.Reconnecting;
                    }

                    try
                    {
                        await ConnectWithoutStartingReconnectAsync(plc);

                        ResetReconnectState(plc.Id);

                        SetStatus(
                            plc.Id,
                            PlcConnectionStatus.Connected);

                        // Reconnect başarılı.
                        // Connection monitoring tekrar başlasın.
                        StartConnectionMonitor(plc);

                        return;
                    }
                    catch (Exception ex)
                    {
                        SetReconnectError(
                            plc.Id,
                            ex);

                        bool shouldFail;

                        lock (_lock)
                        {
                            var state =
                                GetOrCreateStateUnsafe(plc.Id);

                            shouldFail =
                                state.ReconnectAttemptCount >=
                                plc.MaxReconnectAttempts;

                            state.Status = shouldFail
                                ? PlcConnectionStatus.Failed
                                : PlcConnectionStatus.Disconnected;
                        }

                        if (shouldFail)
                            return;

                        // Bir sonraki reconnect denemesi için
                        // loop devam eder.
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SetReconnectError(
                        plc.Id,
                        ex);
                }
            }
        }

        private async Task ConnectWithoutStartingReconnectAsync(Plc plc)
        {
            RemoveConnection(plc.Id);

            S7NetPlc connection = new(
                plc.CpuType,
                plc.IpAddress,
                plc.Rack,
                plc.Slot);

            try
            {
                await Task.Run(() => connection.Open());

                if (!connection.IsConnected)
                    throw new InvalidOperationException(
                        $"{plc.Name} bağlantısı kurulamadı.");

                lock (_lock)
                {
                    _connections[plc.Id] = connection;
                }
            }
            catch
            {
                connection.Close();
                throw;
            }
        }

        private void StopReconnect(int plcId)
        {
            CancellationTokenSource? cts = null;

            lock (_lock)
            {
                if (_reconnectTokens.TryGetValue(plcId, out cts))
                    _reconnectTokens.Remove(plcId);
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private void ResetReconnectState(int plcId)
        {
            lock (_lock)
            {
                var state = GetOrCreateStateUnsafe(plcId);

                state.ReconnectAttemptCount = 0;
                state.LastError = null;
            }
        }

        private void SetReconnectError(int plcId, Exception exception)
        {
            lock (_lock)
            {
                var state = GetOrCreateStateUnsafe(plcId);

                state.ReconnectAttemptCount++;
                state.LastError = exception.Message;
            }
        }

        #endregion

        #region Path Resolution

        public ResolvedAddress ResolveAddress(
            DataBlockVariable variable,
            string path)
        {
            List<PathSegment> segments = ParsePath(path);

            List<DataBlockVariable> hierarchy =
                BuildHierarchy(variable);

            if (segments.Count != hierarchy.Count)
                throw new ArgumentException(
                    "Path variable hiyerarşisiyle eşleşmiyor.");

            int byteOffset = hierarchy[0].ByteOffset;
            byte bitOffset = hierarchy[0].BitOffset;

            string resolvedType = hierarchy[^1].TypeDefinition;

            for (int i = 0; i < hierarchy.Count; i++)
            {
                DataBlockVariable current = hierarchy[i];
                PathSegment segment = segments[i];

                if (!current.Name.Equals(
                        segment.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"'{segment.Name}' segmenti " +
                        $"'{current.Name}' ile eşleşmiyor.");
                }

                if (i > 0)
                {
                    DataBlockVariable parent =
                        hierarchy[i - 1];

                    byteOffset +=
                        current.ByteOffset -
                        parent.ByteOffset;
                }

                if (segment.Index.HasValue)
                {
                    if (!IsArray(current.TypeDefinition))
                    {
                        throw new ArgumentException(
                            $"'{current.Name}' Array değil fakat index verilmiş.");
                    }

                    var bounds =
                        _dataBlockService.GetArrayBounds(
                            current.TypeDefinition);

                    int relativeIndex =
                        segment.Index.Value - bounds.Lower;

                    int count =
                        _dataBlockService.GetArrayCount(
                            current.TypeDefinition);

                    if (relativeIndex < 0 ||
                        relativeIndex >= count)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(path),
                            $"'{current.Name}' index değeri geçersiz.");
                    }

                    string elementType =
                        _dataBlockService.GetArrayElementType(
                            current.TypeDefinition);

                    int elementSize =
                        current.Size / count;

                    byteOffset +=
                        relativeIndex * elementSize;

                    resolvedType = elementType;
                }
                else
                {
                    resolvedType = current.TypeDefinition;
                }
            }

            return new ResolvedAddress
            {
                ByteOffset = byteOffset,
                BitOffset = bitOffset,
                TypeDefinition = resolvedType
            };
        }

        private List<PathSegment> ParsePath(string path)
        {
            MatchCollection matches = Regex.Matches(
                path,
                @"""(?<name>[^""]+)""(?:\[(?<index>-?\d+)\])?");

            if (matches.Count == 0)
            {
                throw new ArgumentException(
                    $"Geçersiz variable path: {path}");
            }

            List<PathSegment> segments = new();

            foreach (Match match in matches)
            {
                int? index = null;

                if (match.Groups["index"].Success)
                {
                    index = int.Parse(
                        match.Groups["index"].Value);
                }

                segments.Add(new PathSegment
                {
                    Name = match.Groups["name"].Value,
                    Index = index
                });
            }

            return segments;
        }

        private class PathSegment
        {
            public string Name { get; set; } = string.Empty;

            public int? Index { get; set; }
        }

        private List<DataBlockVariable> BuildHierarchy(
            DataBlockVariable variable)
        {
            List<DataBlockVariable> hierarchy = new();

            DataBlockVariable? current = variable;

            while (current != null)
            {
                hierarchy.Add(current);
                current = current.Parent;
            }

            hierarchy.Reverse();

            return hierarchy;
        }

        private bool IsArray(string typeDefinition)
        {
            return typeDefinition
                .Trim()
                .StartsWith(
                    "Array",
                    StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Read Operations

        public object? ReadValue(
            Plc plc,
            DataBlock dataBlock,
            string typeDefinition,
            int byteOffset,
            int bitOffset)
        {
            S7NetPlc connection = GetConnection(plc.Id);

            string type = typeDefinition.Trim();

            return type.ToLowerInvariant() switch
            {
                "bool" => connection.Read(
                    DataType.DataBlock,
                    dataBlock.DbNumber,
                    byteOffset,
                    VarType.Bit,
                    1,
                    (byte)bitOffset),

                "byte" => connection.Read(
                    DataType.DataBlock,
                    dataBlock.DbNumber,
                    byteOffset,
                    VarType.Byte,
                    1),

                "word" => connection.Read(
                    DataType.DataBlock,
                    dataBlock.DbNumber,
                    byteOffset,
                    VarType.Word,
                    1),

                "dword" => connection.Read(
                    DataType.DataBlock,
                    dataBlock.DbNumber,
                    byteOffset,
                    VarType.DWord,
                    1),

                "int" => connection.Read(
                    DataType.DataBlock,
                    dataBlock.DbNumber,
                    byteOffset,
                    VarType.Int,
                    1),

                "dint" => connection.Read(
                    DataType.DataBlock,
                    dataBlock.DbNumber,
                    byteOffset,
                    VarType.DInt,
                    1),

                "real" => connection.Read(
                    DataType.DataBlock,
                    dataBlock.DbNumber,
                    byteOffset,
                    VarType.Real,
                    1),

                _ => ReadValueDefault(
                    connection,
                    dataBlock,
                    type,
                    byteOffset)
            };
        }
        private object? ReadValueDefault(
            S7NetPlc connection,
            DataBlock dataBlock,
            string type,
            int byteOffset)
        {
            if (type.StartsWith(
                "String",
                StringComparison.OrdinalIgnoreCase))
            {
                return ReadString(
                    connection,
                    dataBlock.DbNumber,
                    byteOffset,
                    _dataBlockService.GetDataTypeSize(type));
            }

            if (type.StartsWith(
                "Array",
                StringComparison.OrdinalIgnoreCase))
            {
                return ReadArray(
                    connection,
                    dataBlock.DbNumber,
                    type,
                    byteOffset);
            }

            throw new NotSupportedException(
                $"Desteklenmeyen veri tipi: {type}");
        }

        public object? ReadVariable(
            Plc plc,
            DataBlock dataBlock,
            DataBlockVariable variable)
        {
            return ReadValue(
                plc,
                dataBlock,
                variable.TypeDefinition,
                variable.ByteOffset,
                variable.BitOffset);
        }

        private string ReadString(
            S7NetPlc connection,
            int dbNumber,
            int byteOffset,
            int size)
        {
            byte[] data = connection.ReadBytes(
                DataType.DataBlock,
                dbNumber,
                byteOffset,
                size);

            if (data.Length < 2)
                return string.Empty;

            int currentLength = data[1];

            if (currentLength <= 0)
                return string.Empty;

            if (currentLength > data.Length - 2)
                currentLength = data.Length - 2;

            return Encoding.ASCII.GetString(data, 2, currentLength);
        }

        private object? ReadArray(
            S7NetPlc connection,
            int dbNumber,
            string type,
            int byteOffset)
        {
            int count =
                _dataBlockService.GetArrayCount(type);

            string elementType =
                _dataBlockService.GetArrayElementType(type);

            return elementType.ToLowerInvariant() switch
            {
                "bool" => connection.Read(
                    DataType.DataBlock,
                    dbNumber,
                    byteOffset,
                    VarType.Bit,
                    count,
                    0),

                "byte" => connection.Read(
                    DataType.DataBlock,
                    dbNumber,
                    byteOffset,
                    VarType.Byte,
                    count),

                "word" => connection.Read(
                    DataType.DataBlock,
                    dbNumber,
                    byteOffset,
                    VarType.Word,
                    count),

                "dword" => connection.Read(
                    DataType.DataBlock,
                    dbNumber,
                    byteOffset,
                    VarType.DWord,
                    count),

                "int" => connection.Read(
                    DataType.DataBlock,
                    dbNumber,
                    byteOffset,
                    VarType.Int,
                    count),

                "dint" => connection.Read(
                    DataType.DataBlock,
                    dbNumber,
                    byteOffset,
                    VarType.DInt,
                    count),

                "real" => connection.Read(
                    DataType.DataBlock,
                    dbNumber,
                    byteOffset,
                    VarType.Real,
                    count),

                _ => throw new NotSupportedException(
                    $"Desteklenmeyen Array eleman tipi: {elementType}")
            };
        }

        #endregion

        #region Write Operations

        public void WriteValue(
            Plc plc,
            DataBlock dataBlock,
            string typeDefinition,
            int byteOffset,
            int bitOffset,
            object value)
        {
            S7NetPlc connection = GetConnection(plc.Id);

            string type = typeDefinition.Trim();

            switch (type.ToLowerInvariant())
            {
                case "bool":
                    connection.Write(
                        DataType.DataBlock,
                        dataBlock.DbNumber,
                        byteOffset,
                        Convert.ToBoolean(value),
                        bitOffset);
                    return;

                case "byte":
                    connection.Write(
                        DataType.DataBlock,
                        dataBlock.DbNumber,
                        byteOffset,
                        Convert.ToByte(value));
                    return;

                case "word":
                    connection.Write(
                        DataType.DataBlock,
                        dataBlock.DbNumber,
                        byteOffset,
                        Convert.ToUInt16(value));
                    return;

                case "dword":
                    connection.Write(
                        DataType.DataBlock,
                        dataBlock.DbNumber,
                        byteOffset,
                        Convert.ToUInt32(value));
                    return;

                case "int":
                    connection.Write(
                        DataType.DataBlock,
                        dataBlock.DbNumber,
                        byteOffset,
                        Convert.ToInt16(value));
                    return;

                case "dint":
                    connection.Write(
                        DataType.DataBlock,
                        dataBlock.DbNumber,
                        byteOffset,
                        Convert.ToInt32(value));
                    return;

                case "real":
                    connection.Write(
                        DataType.DataBlock,
                        dataBlock.DbNumber,
                        byteOffset,
                        Convert.ToSingle(value));
                    return;

                default:
                    WriteValueDefault(
                        connection,
                        dataBlock,
                        type,
                        byteOffset,
                        value);
                    return;
            }
        }

        private void WriteValueDefault(
            S7NetPlc connection,
            DataBlock dataBlock,
            string type,
            int byteOffset,
            object value)
        {
            if (type.StartsWith(
                "String",
                StringComparison.OrdinalIgnoreCase))
            {
                WriteString(
                    connection,
                    dataBlock.DbNumber,
                    byteOffset,
                    _dataBlockService.GetDataTypeSize(type),
                    Convert.ToString(value) ?? string.Empty);

                return;
            }

            if (type.StartsWith(
                "Array",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Array değişkeninin tamamına WriteValue ile yazılamaz. " +
                    "Belirli bir array elemanı için ResolveAddress kullanılmalıdır.");
            }

            throw new NotSupportedException(
                $"Desteklenmeyen veri tipi: {type}");
        }

        public void WriteVariable(
            Plc plc,
            DataBlock dataBlock,
            DataBlockVariable variable,
            object value)
        {
            WriteValue(
                plc,
                dataBlock,
                variable.TypeDefinition,
                variable.ByteOffset,
                variable.BitOffset,
                value);
        }

        private void WriteString(
            S7NetPlc connection,
            int dbNumber,
            int byteOffset,
            int size,
            string value)
        {
            if (size < 2)
                throw new ArgumentException(
                    "String boyutu en az 2 byte olmalıdır.");

            int maxLength = size - 2;

            if (value.Length > maxLength)
                throw new ArgumentException(
                    $"String en fazla {maxLength} karakter olabilir.");

            byte[] data = new byte[size];
            data[0] = (byte)maxLength;
            data[1] = (byte)value.Length;

            byte[] text = Encoding.ASCII.GetBytes(value);
            Array.Copy(text, 0, data, 2, text.Length);

            connection.WriteBytes(
                DataType.DataBlock,
                dbNumber,
                byteOffset,
                data);
        }
        
        #endregion
    }
}