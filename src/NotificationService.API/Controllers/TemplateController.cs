using Microsoft.AspNetCore.Mvc;
using NotificationService.Domain.Repositories;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplateController : ControllerBase
{
    private readonly ITemplateRepository _templateRepository;

    public TemplateController(ITemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await _templateRepository.GetAllAsync();
        return Ok(templates.Select(t => new
        {
            id = t.Id,
            name = t.Name,
            title = t.TitleTemplate,
            message = t.MessageTemplate,
            channels = t.Channels
        }));
    }
}
