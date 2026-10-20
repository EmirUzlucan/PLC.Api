using Microsoft.EntityFrameworkCore;
using PLC.Api.Data;
using PLC.Api.DTOs.Plc;
using PLC.Api.Models;
using System.Reflection.Metadata;
using System.Text.Json;

namespace PLC.Api.Services
{
    public class PlcApiService : IPlcApiService
    {
        private readonly AppDbContext _context;
        private readonly IPlcService _plcService;
        private readonly IDataBlockService _dataBlockService;

        public PlcApiService(
            AppDbContext context,
            IPlcService plcService,
            IDataBlockService dataBlockService)
        {
            _context = context;
            _plcService = plcService;
            _dataBlockService = dataBlockService;
        }

        #region PLC CRUD

        public async Task<List<PlcResponse>> GetAllAsync()
        {
            var plcs = await _context.Plcs
                .ToListAsync();

            return plcs
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<PlcResponse?> GetByIdAsync(int plcId)
        {
            var plc = await _context.Plcs
                .FirstOrDefaultAsync(p => p.Id == plcId);

            if (plc == null)
                return null;

            return MapToResponse(plc);
        }

        public async Task<PlcResponse> CreateAsync(
            CreatePlcRequest request)
        {
            short rack;
            short slot;

            S7.Net.CpuType cpuType = ParseCpuType(request.CpuType);

            if (request.Rack.HasValue &&
                request.Slot.HasValue)
            {
                rack = request.Rack.Value;
                slot = request.Slot.Value;
            }
            else
            {
                var defaults = GetDefaultRackSlot(cpuType);

                rack = defaults.Rack;
                slot = defaults.Slot;
            }

            var plc = new Plc
            {
                Name = request.Name,
                IpAddress = request.IpAddress,
                CpuType = cpuType,
                Rack = rack,
                Slot = slot,
                AutoReconnect = request.AutoReconnect,
                ReconnectIntervalMs = request.ReconnectIntervalMs,
                MaxReconnectAttempts = request.MaxReconnectAttempts
            };

            _context.Plcs.Add(plc);

            await _context.SaveChangesAsync();

            return MapToResponse(plc);
        }

        private S7.Net.CpuType ParseCpuType(int value)
        {
            if (!Enum.IsDefined(typeof(S7.Net.CpuType), value))
                throw new ArgumentException($"Geçersiz CPU tipi: {value}");

            return (S7.Net.CpuType)value;
        }

        private (short Rack, short Slot) GetDefaultRackSlot(S7.Net.CpuType cpuType)
        {
            return cpuType switch
            {
                S7.Net.CpuType.S71200 => (0, 0),
                S7.Net.CpuType.S71500 => (0, 1),

                _ => throw new InvalidOperationException(
                    $"{cpuType} için varsayılan Rack/Slot tanımlı değil. " +
                    "Rack ve Slot değerlerini manuel olarak giriniz.")
            };
        }

        public async Task<PlcResponse?> UpdateAsync(
            int plcId,
            UpdatePlcRequest request)
        {
            var plc = await _context.Plcs
                .FirstOrDefaultAsync(p => p.Id == plcId);

            if (plc == null)
                return null;

            if (request.Name != null)
                plc.Name = request.Name;

            if (request.IpAddress != null)
                plc.IpAddress = request.IpAddress;

            if (request.CpuType.HasValue)
                plc.CpuType = ParseCpuType(request.CpuType.Value);

            if (request.Rack.HasValue)
                plc.Rack = request.Rack.Value;

            if (request.Slot.HasValue)
                plc.Slot = request.Slot.Value;

            if (request.AutoReconnect.HasValue)
                plc.AutoReconnect = request.AutoReconnect.Value;

            if (request.MaxReconnectAttempts.HasValue)
                plc.MaxReconnectAttempts = request.MaxReconnectAttempts.Value;

            if (request.ReconnectIntervalMs.HasValue)
                plc.ReconnectIntervalMs = request.ReconnectIntervalMs.Value;

            await _context.SaveChangesAsync();

            return MapToResponse(plc);
        }

        public async Task<bool> DeleteAsync(int plcId)
        {
            var plc = await _context.Plcs
                .FirstOrDefaultAsync(p => p.Id == plcId);

            if (plc == null)
                return false;

            _context.Plcs.Remove(plc);

            await _context.SaveChangesAsync();

            return true;
        }

        #endregion

        public List<PlcCpuTypeResponse> GetCpuTypes()
        {
            return new List<PlcCpuTypeResponse>
            {
                new()
                {
                    Name = "S7-200",
                    Value = (int)S7.Net.CpuType.S7200
                },
                new()
                {
                    Name = "S7-200 Smart",
                    Value = (int)S7.Net.CpuType.S7200Smart
                },
                new()
                {
                    Name = "LOGO! 0BA8",
                    Value = (int)S7.Net.CpuType.Logo0BA8
                },
                new()
                {
                    Name = "S7-300",
                    Value = (int)S7.Net.CpuType.S7300
                },
                new()
                {
                    Name = "S7-400",
                    Value = (int)S7.Net.CpuType.S7400
                },
                new()
                {
                    Name = "S7-1200",
                    Value = (int)S7.Net.CpuType.S71200
                },
                new()
                {
                    Name = "S7-1500",
                    Value = (int)S7.Net.CpuType.S71500
                }
            };
        }

        #region Connection

        public async Task<PlcStatusResponse> GetStatusAsync(
            int plcId)
        {
            var plc = await GetPlcAsync(plcId);
            var state = _plcService.GetConnectionState(plcId);

            return new PlcStatusResponse
            {
                PlcId = plc.Id,
                Status = state.Status.ToString(),
                ReconnectAttemptCount = state.ReconnectAttemptCount,
                LastError = state.LastError
            };
        }

        public async Task<PlcStatusResponse> ConnectAsync(
            int plcId)
        {
            var plc = await GetPlcAsync(plcId);

            await _plcService.ConnectAsync(plc);

            var state = _plcService.GetConnectionState(plcId);

            return new PlcStatusResponse
            {
                PlcId = plc.Id,
                Status = state.Status.ToString(),
            };
        }

        public async Task<PlcStatusResponse> DisconnectAsync(
            int plcId)
        {
            var plc = await GetPlcAsync(plcId);

            _plcService.Disconnect(plc.Id);

            var state = _plcService.GetConnectionState(plcId);

            return new PlcStatusResponse
            {
                PlcId = plc.Id,
                Status = state.Status.ToString(),
            };
        }

        #endregion

        #region Variable Read

        public async Task<VariableValueResponse?> ReadVariableAsync(
            int variableId,
            string? path = null)
        {
            var variable = await GetVariableAsync(variableId);

            if (variable == null)
                return null;

            int byteOffset = variable.ByteOffset;
            byte bitOffset = variable.BitOffset;
            string typeDefinition = variable.TypeDefinition;

            object? value;

            if (string.IsNullOrWhiteSpace(path))
            {
                value = _plcService.ReadVariable(
                    variable.DataBlock.Plc,
                    variable.DataBlock,
                    variable);
            }
            else
            {
                var address = _plcService.ResolveAddress(
                    variable,
                    path);

                byteOffset = address.ByteOffset;
                bitOffset = address.BitOffset;
                typeDefinition = address.TypeDefinition;

                value = _plcService.ReadValue(
                    variable.DataBlock.Plc,
                    variable.DataBlock,
                    typeDefinition,
                    byteOffset,
                    bitOffset);
            }

            return new VariableValueResponse
            {
                VariableId = variable.Id,
                Name = variable.Name,
                Path = string.IsNullOrWhiteSpace(path)
                    ? variable.Path
                    : path,
                TypeDefinition = variable.TypeDefinition,
                ByteOffset = byteOffset,
                BitOffset = bitOffset,
                Value = value
            };
        }

        #endregion

        #region Variable Write

        public async Task<bool> WriteVariableAsync(
            int variableId,
            WriteVariableRequest request,
            string? path = null)
        {
            var variable = await GetVariableAsync(variableId);

            if (variable == null)
                return false;

            object value = ConvertWriteValue(request.Value, variable.TypeDefinition);

            if (string.IsNullOrWhiteSpace(path))
            {
                _plcService.WriteVariable(
                    variable.DataBlock.Plc,
                    variable.DataBlock,
                    variable,
                    value);

                return true;
            }

            var address = _plcService.ResolveAddress(
                variable,
                path);

            _plcService.WriteValue(
                variable.DataBlock.Plc,
                variable.DataBlock,
                address.TypeDefinition,
                address.ByteOffset,
                address.BitOffset,
                value);

            return true;
        }

        #endregion

        #region Variable Helpers

        private async Task<DataBlockVariable?> GetVariableAsync(
            int variableId)
        {
            var variable = await _context.DataBlockVariables
                .Include(v => v.DataBlock)
                    .ThenInclude(db => db.Plc)
                .FirstOrDefaultAsync(v => v.Id == variableId);

            if (variable == null)
                return null;

            var variables = await _context.DataBlockVariables
                .Where(v => v.DataBlockId == variable.DataBlockId)
                .ToListAsync();

            var lookup = variables.ToDictionary(v => v.Id);

            foreach (var item in variables)
            {
                if (item.ParentId.HasValue &&
                    lookup.TryGetValue(item.ParentId.Value, out var parent))
                {
                    item.Parent = parent;
                }
            }

            var target = lookup[variable.Id];

            target.DataBlock = variable.DataBlock;

            return target;
        }

        private async Task<Plc> GetPlcAsync(int plcId)
        {
            var plc = await _context.Plcs
                .FirstOrDefaultAsync(p => p.Id == plcId);

            if (plc == null)
            {
                throw new KeyNotFoundException(
                    $"PLC bulunamadı: {plcId}");
            }

            return plc;
        }

        private object ConvertWriteValue(
            object value,
            string typeDefinition)
        {
            if (value is not JsonElement json)
                return value;

            string type = typeDefinition.Trim();

            return type.ToLowerInvariant() switch
            {
                "bool" => json.GetBoolean(),

                "byte" => json.GetByte(),

                "word" => json.GetUInt16(),

                "dword" => json.GetUInt32(),

                "int" => json.GetInt16(),

                "dint" => json.GetInt32(),

                "real" => json.GetSingle(),

                _ when type.StartsWith(
                    "String",
                    StringComparison.OrdinalIgnoreCase)
                    => json.GetString() ?? string.Empty,

                _ => throw new NotSupportedException(
                    $"Write için desteklenmeyen veri tipi: {type}")
            };
        }

        #endregion

        #region Mapping

        private PlcResponse MapToResponse(Plc plc)
        {
            return new PlcResponse
            {
                Id = plc.Id,
                Name = plc.Name,
                IpAddress = plc.IpAddress,
                CpuType = (int)plc.CpuType,
                Rack = plc.Rack,
                Slot = plc.Slot,
                AutoReconnect = plc.AutoReconnect,
                MaxReconnectAttempts = plc.MaxReconnectAttempts,
                ReconnectIntervalMs = plc.ReconnectIntervalMs
            };
        }

        #endregion
    }
}