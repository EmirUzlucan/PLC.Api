using Microsoft.AspNetCore.Mvc;
using PLC.Api.DTOs.Plc;
using PLC.Api.Services;

namespace PLC.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlcController : ControllerBase
    {
        private readonly IPlcApiService _plcApiService;

        public PlcController(IPlcApiService plcApiService)
        {
            _plcApiService = plcApiService;
        }

        #region PLC CRUD

        [HttpGet]
        public async Task<ActionResult<List<PlcResponse>>> GetAll()
        {
            var plcs = await _plcApiService.GetAllAsync();

            return Ok(plcs);
        }

        [HttpGet("{plcId:int}")]
        public async Task<ActionResult<PlcResponse>> GetById(
            int plcId)
        {
            var plc = await _plcApiService.GetByIdAsync(plcId);

            if (plc == null)
                return NotFound();

            return Ok(plc);
        }

        [HttpPost]
        public async Task<ActionResult<PlcResponse>> Create(
            CreatePlcRequest request)
        {
            try
            {
                var plc =
                    await _plcApiService.CreateAsync(request);

                return CreatedAtAction(
                    nameof(GetById),
                    new { plcId = plc.Id },
                    plc);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    error = ex.Message
                });
            }
        }

        [HttpPatch("{plcId:int}")]
        public async Task<ActionResult<PlcResponse>> Update(
            int plcId,
            UpdatePlcRequest request)
        {
            var plc = await _plcApiService.UpdateAsync(
                plcId,
                request);

            if (plc == null)
                return NotFound();

            return Ok(plc);
        }

        [HttpDelete("{plcId:int}")]
        public async Task<IActionResult> Delete(
            int plcId)
        {
            bool deleted = await _plcApiService.DeleteAsync(plcId);

            if (!deleted)
                return NotFound();

            return NoContent();
        }

        #endregion

        [HttpGet("cpu-types")]
        public ActionResult<List<PlcCpuTypeResponse>> GetCpuTypes()
        {
            return Ok(_plcApiService.GetCpuTypes());
        }

        #region Connection

        [HttpGet("{plcId:int}/status")]
        public async Task<ActionResult<PlcStatusResponse>> GetStatus(
            int plcId)
        {
            var status = await _plcApiService.GetStatusAsync(plcId);

            return Ok(status);
        }

        [HttpPost("{plcId:int}/connect")]
        public async Task<ActionResult<PlcStatusResponse>> Connect(
            int plcId)
        {
            var status = await _plcApiService.ConnectAsync(plcId);

            return Ok(status);
        }

        [HttpPost("{plcId:int}/disconnect")]
        public async Task<ActionResult<PlcStatusResponse>> Disconnect(
            int plcId)
        {
            var status = await _plcApiService.DisconnectAsync(plcId);

            return Ok(status);
        }

        #endregion

        #region Variable

        [HttpGet("variable/{variableId:int}")]
        public async Task<ActionResult<VariableValueResponse>> ReadVariable(
            int variableId,
            [FromQuery] string? path)
        {
            var result = await _plcApiService.ReadVariableAsync(
                variableId,
                path);

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPut("variable/{variableId:int}")]
        public async Task<IActionResult> WriteVariable(
            int variableId,
            [FromQuery] string? path,
            [FromBody] WriteVariableRequest request)
        {
            bool success = await _plcApiService.WriteVariableAsync(
                variableId,
                request,
                path);

            if (!success)
                return NotFound();

            return NoContent();
        }

        #endregion
    }
}