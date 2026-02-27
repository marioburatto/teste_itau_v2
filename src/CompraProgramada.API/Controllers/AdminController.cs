using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CompraProgramada.API.Controllers;

[ApiController]
[Route("api/admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly CestaService _cestaService;
    private readonly MotorCompraService _motorCompraService;

    public AdminController(CestaService cestaService, MotorCompraService motorCompraService)
    {
        _cestaService = cestaService;
        _motorCompraService = motorCompraService;
    }

    [HttpPost("cesta")]
    [ProducesResponseType(typeof(CestaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CadastrarCesta([FromBody] CestaRequest request)
    {
        var result = await _cestaService.CadastrarOuAlterarAsync(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("cesta/atual")]
    [ProducesResponseType(typeof(CestaAtualResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCestaAtual()
    {
        var result = await _cestaService.ObterAtualAsync();
        return Ok(result);
    }

    [HttpGet("cesta/historico")]
    [ProducesResponseType(typeof(HistoricoCestasResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterHistoricoCestas()
    {
        var result = await _cestaService.ObterHistoricoAsync();
        return Ok(result);
    }

    [HttpGet("conta-master/custodia")]
    [ProducesResponseType(typeof(CustodiaMasterResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsultarCustodiaMaster()
    {
        var result = await _motorCompraService.ConsultarCustodiaMasterAsync();
        return Ok(result);
    }
}
