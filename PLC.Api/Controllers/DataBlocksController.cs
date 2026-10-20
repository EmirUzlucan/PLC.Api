using Microsoft.AspNetCore.Mvc;
using PLC.Api.DTOs.DataBlock;

namespace PLC.Api.Controllers
{
    [ApiController]
    [Route("api")]
    public class DataBlocksController : ControllerBase
    {
        private readonly IDataBlockApiService _dataBlockApiService;

        public DataBlocksController(
            IDataBlockApiService dataBlockApiService)
        {
            _dataBlockApiService = dataBlockApiService;
        }

        // GET /api/datablocks
        [HttpGet("datablocks")]
        public async Task<ActionResult<List<DataBlockResponse>>> GetAll()
        {
            var dataBlocks =
                await _dataBlockApiService.GetAllAsync();

            return Ok(dataBlocks);
        }

        // GET /api/plc/{plcId}/datablocks
        [HttpGet("plc/{plcId:int}/datablocks")]
        public async Task<ActionResult<List<DataBlockResponse>>> GetByPlcId(
            int plcId)
        {
            try
            {
                var dataBlocks =
                    await _dataBlockApiService.GetByPlcIdAsync(plcId);

                return Ok(dataBlocks);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // GET /api/datablocks/{dbId}
        [HttpGet("datablocks/{dbId:int}")]
        public async Task<ActionResult<DataBlockDetailResponse>> GetById(
            int dbId)
        {
            var dataBlock =
                await _dataBlockApiService.GetByIdAsync(dbId);

            if (dataBlock == null)
                return NotFound();

            return Ok(dataBlock);
        }

        // GET /api/datablocks/{dbId}/variables
        [HttpGet("datablocks/{dbId:int}/variables")]
        public async Task<ActionResult<List<DataBlockVariableResponse>>> GetVariables(
            int dbId)
        {
            try
            {
                var variables =
                    await _dataBlockApiService.GetVariablesAsync(dbId);

                return Ok(variables);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // GET /api/datablocks/{dbId}/variables/root
        [HttpGet("datablocks/{dbId:int}/variables/root")]
        public async Task<ActionResult<List<VariableChildResponse>>> GetRootVariables(
            int dbId)
        {
            try
            {
                var variables =
                    await _dataBlockApiService.GetRootVariablesAsync(dbId);

                return Ok(variables);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // GET /api/datablocks/{dbId}/variables/{variableId}
        [HttpGet("datablocks/{dbId:int}/variables/{variableId:int}")]
        public async Task<ActionResult<DataBlockVariable>> GetVariable(
            int dbId,
            int variableId)
        {
            try
            {
                var variable =
                    await _dataBlockApiService.GetVariableAsync(
                        dbId,
                        variableId);

                if (variable == null)
                    return NotFound(new { message = "Variable bulunamadı." });

                return Ok(variable);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // GET /api/datablocks/{dbId}/variables/{variableId}/children?path=...
        [HttpGet("datablocks/{dbId:int}/variables/{variableId:int}/children")]
        public async Task<ActionResult<List<VariableChildResponse>>> GetVariableChildren(
            int dbId,
            int variableId,
            [FromQuery] string path)
        {
            try
            {
                var children =
                    await _dataBlockApiService.GetVariableChildrenAsync(
                        dbId,
                        variableId,
                        path);

                return Ok(children);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // POST /api/datablocks
        [HttpPost("datablocks")]
        public async Task<ActionResult<DataBlockDetailResponse>> Create(
            [FromForm] CreateDataBlockRequest request)
        {
            try
            {
                var dataBlock =
                    await _dataBlockApiService.CreateAsync(request);

                return CreatedAtAction(
                    nameof(GetById),
                    new { dbId = dataBlock.Id },
                    dataBlock);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
            catch (FormatException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // PATCH /api/datablocks/{dbId}
        [HttpPatch("datablocks/{dbId:int}")]
        public async Task<ActionResult<DataBlockDetailResponse>> Update(
            int dbId,
            [FromForm] UpdateDataBlockRequest request)
        {
            try
            {
                var dataBlock =
                    await _dataBlockApiService.UpdateAsync(
                        dbId,
                        request);

                if (dataBlock == null)
                    return NotFound();

                return Ok(dataBlock);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
            catch (FormatException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // DELETE /api/datablocks/{dbId}
        [HttpDelete("datablocks/{dbId:int}")]
        public async Task<IActionResult> Delete(int dbId)
        {
            var deleted =
                await _dataBlockApiService.DeleteAsync(dbId);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}