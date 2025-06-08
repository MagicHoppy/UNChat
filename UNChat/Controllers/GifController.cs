using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/[controller]")]
public class GifController : ControllerBase
{
    private readonly string _tenorApiKey;
    private readonly HttpClient _httpClient;

    public GifController(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _tenorApiKey = config["Tenor:ApiKey"];
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet("search")]
    [SwaggerOperation(Summary = "Search GIFs", Description = "Searches for GIFs using the Tenor API")]
    [SwaggerResponse(200, "GIF search results returned successfully")]
    [SwaggerResponse(400, "Empty search query")]
    [SwaggerResponse(500, "Error fetching GIFs from Tenor API")]
    public async Task<IActionResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Zapytanie nie może być puste.");

        var url = $"https://tenor.googleapis.com/v2/search?q={Uri.EscapeDataString(query)}&key={_tenorApiKey}&limit=10";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Błąd pobierania GIF-ów z Tenora.");

        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }
}