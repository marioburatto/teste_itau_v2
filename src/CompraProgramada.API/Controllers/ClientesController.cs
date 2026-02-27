using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CompraProgramada.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Produces("application/json")]
public class ClientesController : ControllerBase
{
    private readonly ClienteService _clienteService;

    public ClientesController(ClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    [HttpPost("adesao")]
    [ProducesResponseType(typeof(AdesaoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Aderir([FromBody] AdesaoRequest request)
    {
        var result = await _clienteService.AderirAsync(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{clienteId}/saida")]
    [ProducesResponseType(typeof(SaidaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Sair(int clienteId)
    {
        var result = await _clienteService.SairAsync(clienteId);
        return Ok(result);
    }

    [HttpPut("{clienteId}/valor-mensal")]
    [ProducesResponseType(typeof(AlterarValorMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AlterarValorMensal(int clienteId, [FromBody] AlterarValorMensalRequest request)
    {
        var result = await _clienteService.AlterarValorMensalAsync(clienteId, request);
        return Ok(result);
    }

    [HttpGet("{clienteId}/carteira")]
    [ProducesResponseType(typeof(CarteiraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarCarteira(int clienteId)
    {
        var result = await _clienteService.ConsultarCarteiraAsync(clienteId);
        return Ok(result);
    }

    [HttpGet("{clienteId}/rentabilidade")]
    [ProducesResponseType(typeof(RentabilidadeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarRentabilidade(int clienteId)
    {
        var result = await _clienteService.ConsultarRentabilidadeAsync(clienteId);
        return Ok(result);
    }
}
