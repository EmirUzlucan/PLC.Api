using Microsoft.EntityFrameworkCore;
using PLC.Api.Data;
using PLC.Api.DTOs.DataBlock;
using PLC.Api.Models;
using S7.Net;

namespace PLC.Api.Services
{
    public class DataBlockApiService : IDataBlockApiService
    {
        private readonly AppDbContext _context;
        private readonly IDataBlockService _dataBlockService;

        public DataBlockApiService(
            AppDbContext context,
            IDataBlockService dataBlockService)
        {
            _context = context;
            _dataBlockService = dataBlockService;
        }

        #region DataBlock CRUD

        public async Task<List<DataBlockResponse>> GetAllAsync()
        {
            var dataBlocks = await _context.DataBlocks
                .ToListAsync();

            return dataBlocks
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<List<DataBlockResponse>> GetByPlcIdAsync(
            int plcId)
        {
            // PLC var mı kontrol et. Yoksa 404 için KeyNotFoundException fırlat.
            var plcExists = await _context.Plcs
                .AnyAsync(p => p.Id == plcId);

            if (!plcExists)
            {
                throw new KeyNotFoundException($"PLC bulunamadı: {plcId}");
            }

            var dataBlocks = await _context.DataBlocks
                .Where(db => db.PlcId == plcId)
                .ToListAsync();

            return dataBlocks
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<DataBlockResponse?> GetByIdAsync(
            int dbId)
        {
            var dataBlock = await _context.DataBlocks
                .FirstOrDefaultAsync(db => db.Id == dbId);

            if (dataBlock == null)
                return null;

            return MapToResponse(dataBlock);
        }

        public async Task<DataBlockDetailResponse> CreateAsync(
            CreateDataBlockRequest request)
        {
            // PLC gerçekten var mı?
            var plcExists = await _context.Plcs
                .AnyAsync(p => p.Id == request.PlcId);

            if (!plcExists)
            {
                throw new KeyNotFoundException(
                    $"PLC bulunamadı: {request.PlcId}");
            }

            // DB number aynı PLC içerisinde zaten kullanılıyor mu?
            var dbNumberExists = await _context.DataBlocks
                .AnyAsync(db =>
                    db.PlcId == request.PlcId &&
                    db.DbNumber == request.DbNumber);

            if (dbNumberExists)
            {
                throw new InvalidOperationException(
                    $"PLC {request.PlcId} içerisinde " +
                    $"DB{request.DbNumber} zaten mevcut.");
            }

            // .db dosyasını parse et
            await using Stream stream =
                request.File.OpenReadStream();

            var parseResult =
                _dataBlockService.Parse(stream);

            // DataBlock entity
            var dataBlock = new DataBlock
            {
                PlcId = request.PlcId,
                DbNumber = request.DbNumber,
                Name = parseResult.Name
            };

            // Variable'lar ile DataBlock arasındaki ilişkiyi kur
            foreach (var variable in parseResult.Variables)
            {
                variable.DataBlock = dataBlock;
            }

            dataBlock.Variables =
                parseResult.Variables;

            _context.DataBlocks.Add(dataBlock);

            await _context.SaveChangesAsync();

            return MapToDetailResponse(dataBlock);
        }

        public async Task<DataBlockDetailResponse?> UpdateAsync(
            int dbId,
            UpdateDataBlockRequest request)
        {
            var dataBlock = await _context.DataBlocks
                .Include(db => db.Variables)
                .FirstOrDefaultAsync(db => db.Id == dbId);

            if (dataBlock == null)
                return null;

            // PLC değiştirilecekse
            if (request.PlcId.HasValue)
            {
                var plcExists = await _context.Plcs
                    .AnyAsync(p => p.Id == request.PlcId.Value);

                if (!plcExists)
                {
                    throw new KeyNotFoundException(
                        $"PLC bulunamadı: {request.PlcId.Value}");
                }

                dataBlock.PlcId =
                    request.PlcId.Value;
            }

            // DB Number değiştirilecekse
            if (request.DbNumber.HasValue)
            {
                int newDbNumber =
                    request.DbNumber.Value;

                bool exists = await _context.DataBlocks
                    .AnyAsync(db =>
                        db.Id != dbId &&
                        db.PlcId == dataBlock.PlcId &&
                        db.DbNumber == newDbNumber);

                if (exists)
                {
                    throw new InvalidOperationException(
                        $"PLC {dataBlock.PlcId} içerisinde " +
                        $"DB{newDbNumber} zaten mevcut.");
                }

                dataBlock.DbNumber = newDbNumber;
            }

            // Yeni .db dosyası gönderildiyse
            if (request.File != null)
            {
                await using Stream stream =
                    request.File.OpenReadStream();

                var parseResult =
                    _dataBlockService.Parse(stream);

                // Eski variable'ları kaldır
                _context.DataBlockVariables.RemoveRange(
                    dataBlock.Variables);

                // Yeni DataBlock adı
                dataBlock.Name =
                    parseResult.Name;

                // Yeni variable'ları ekle
                dataBlock.Variables =
                    parseResult.Variables;

                // İlişkiyi navigation property üzerinden kur
                foreach (var variable in dataBlock.Variables)
                {
                    variable.DataBlock = dataBlock;
                }
            }

            await _context.SaveChangesAsync();

            return MapToDetailResponse(dataBlock);
        }

        public async Task<bool> DeleteAsync(
            int dbId)
        {
            var dataBlock = await _context.DataBlocks
                .FirstOrDefaultAsync(db => db.Id == dbId);

            if (dataBlock == null)
                return false;

            _context.DataBlocks.Remove(dataBlock);

            await _context.SaveChangesAsync();

            return true;
        }
        
        #endregion

        #region Variable

        public async Task<List<DataBlockVariableResponse>> GetVariablesAsync(
            int dbId)
        {
            var dataBlockExists = await _context.DataBlocks
                .AnyAsync(db => db.Id == dbId);

            if (!dataBlockExists)
                throw new KeyNotFoundException($"DataBlock bulunamadı: {dbId}");

            var Variables = await _context.DataBlockVariables
                .Where(v => v.DataBlockId == dbId)
                .ToListAsync();

            return Variables
                .Select(MapToVariableResponse)
                .ToList();
        }

        public async Task<DataBlockVariableResponse?> GetVariableAsync(
            int dbId,
            int variableId)
        {
            var dataBlockExists = await _context.DataBlocks
                .AnyAsync(db => db.Id == dbId);

            if (!dataBlockExists)
                throw new KeyNotFoundException($"DataBlock bulunamadı: {dbId}");

            var Variable = await _context.DataBlockVariables
                .FirstOrDefaultAsync(v => v.Id == variableId);

            if (Variable == null)
                return null;

            return MapToVariableResponse(Variable);
        }

        public async Task<List<VariableChildResponse>> GetRootVariablesAsync(
            int dbId)
        {
            var dataBlockExists = await _context.DataBlocks
                .AnyAsync(db => db.Id == dbId);

            if (!dataBlockExists)
                throw new KeyNotFoundException($"DataBlock bulunamadı: {dbId}");

            var variables = await _context.DataBlockVariables
                .Where(v =>
                    v.DataBlockId == dbId &&
                    v.ParentId == null)
                .Include(v => v.Children)
                .ToListAsync();

            return variables
                .Select(variable => new VariableChildResponse
                {
                    Id = variable.Id,
                    Name = variable.Name,
                    Path = variable.Path,
                    HasChildren = variable.Children.Any()
                })
                .ToList();
        }

        public async Task<List<VariableChildResponse>> GetVariableChildrenAsync(
            int dbId,
            int variableId,
            string path)
        {
            var variable = await _context.DataBlockVariables
                .Include(v => v.Children)
                .FirstOrDefaultAsync(v =>
                    v.Id == variableId &&
                    v.DataBlockId == dbId);

            if (variable == null)
                throw new KeyNotFoundException(
                    $"Variable bulunamadı: {variableId}");

            return await _dataBlockService.GetVariableChildrenAsync(
                variable,
                path);
        }

        #endregion

        #region Mapping

        private DataBlockResponse MapToResponse(
            DataBlock dataBlock)
        {
            return new DataBlockResponse
            {
                Id = dataBlock.Id,
                PlcId = dataBlock.PlcId,
                DbNumber = dataBlock.DbNumber,
                Name = dataBlock.Name
            };
        }

        private DataBlockDetailResponse MapToDetailResponse(
            DataBlock dataBlock)
        {
            return new DataBlockDetailResponse
            {
                Id = dataBlock.Id,
                PlcId = dataBlock.PlcId,
                DbNumber = dataBlock.DbNumber,
                Name = dataBlock.Name,

                Variables = dataBlock.Variables
                    .Select(variable => new DataBlockVariableResponse
                    {
                        Id = variable.Id,
                        Name = variable.Name,
                        Path = variable.Path,
                        TypeDefinition = variable.TypeDefinition,
                        ByteOffset = variable.ByteOffset,
                        BitOffset = variable.BitOffset,
                        Size = variable.Size
                    })
                    .ToList()
            };
        }

        private DataBlockVariableResponse MapToVariableResponse(
            DataBlockVariable variable)
        {
            return new DataBlockVariableResponse
            {
                Id = variable.Id,
                Name = variable.Name,
                Path = variable.Path,
                TypeDefinition =
                            variable.TypeDefinition,
                ByteOffset =
                            variable.ByteOffset,
                BitOffset =
                            variable.BitOffset,

                Size = variable.Size
            };
        }

        #endregion
    }
}