using Esotera.Application.DTOs.Newsletter;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/newsletter")]
public class NewsletterController : ControllerBase
{
    private readonly INewsletterService _newsletter;

    public NewsletterController(INewsletterService newsletter)
    {
        _newsletter = newsletter;
    }

    [HttpPost("subscribe")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<NewsletterMessageResponse>> Subscribe(
        [FromBody] SubscribeNewsletterRequest request)
    {
        var result = await _newsletter.SubscribeAsync(request);
        return Ok(result);
    }

    [HttpPost("unsubscribe")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<NewsletterMessageResponse>> Unsubscribe(
        [FromBody] UnsubscribeNewsletterRequest request)
    {
        var result = await _newsletter.UnsubscribeAsync(request.Token);
        return Ok(result);
    }

    [HttpGet("unsubscribe")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<NewsletterMessageResponse>> UnsubscribeGet(
        [FromQuery] string token)
    {
        var result = await _newsletter.UnsubscribeAsync(token);
        return Ok(result);
    }
}

public record UnsubscribeNewsletterRequest(string Token);
