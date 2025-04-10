using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class GifController : ControllerBase
{
    private readonly string _tenorApiKey;

    public GifController(IConfiguration config)
    {
        _tenorApiKey = config["Tenor:ApiKey"];
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Zapytanie nie może być puste.");

        var url = $"https://tenor.googleapis.com/v2/search?q={Uri.EscapeDataString(query)}&key={_tenorApiKey}&limit=10";

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Błąd pobierania GIF-ów z Tenora.");

        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

}

