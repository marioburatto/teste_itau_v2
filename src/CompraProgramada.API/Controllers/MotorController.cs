using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CompraProgramada.API.Controllers;

[ApiController]
[Route("api/motor")]
[Produces("application/json")]
public class MotorController : ControllerBase
{
    private readonly MotorCompraService _motorCompraService;

    public MotorController(MotorCompraService motorCompraService)
    {
        _motorCompraService = motorCompraService;
    }

    [HttpPost("executar-compra")]
    [ProducesResponseType(typeof(ExecutarCompraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecutarCompra([FromBody] ExecutarCompraRequest request)
    {
        if (!DateTime.TryParse(request.DataReferencia, out var dataReferencia))
            return BadRequest(new ErrorResponse
            {
                Erro = "Data de referencia invalida. Use o formato yyyy-MM-dd.",
                Codigo = "DATA_INVALIDA"
            });

        var result = await _motorCompraService.ExecutarCompraAsync(dataReferencia);
        return Ok(result);
    }
}
