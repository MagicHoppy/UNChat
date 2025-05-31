using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UNChat.Controllers; // Adjust namespace
using Moq.Protected;

public class GifControllerTests
{
    private GifController CreateController(HttpResponseMessage fakeResponse)
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Tenor:ApiKey"]).Returns("test-api-key");

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(fakeResponse);

        var httpClient = new HttpClient(handlerMock.Object);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        return new GifController(mockConfig.Object, httpClientFactory.Object);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenQueryIsEmpty()
    {
        var controller = CreateController(new HttpResponseMessage());

        var result = await controller.Search("   ");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Zapytanie nie może być puste.", badRequest.Value);
    }

    [Fact]
    public async Task Search_ReturnsErrorStatus_WhenTenorFails()
    {
        var fakeResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var controller = CreateController(fakeResponse);

        var result = await controller.Search("dogs");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Błąd pobierania GIF-ów z Tenora.", statusResult.Value);
    }

    [Fact]
    public async Task Search_ReturnsJsonContent_WhenSuccess()
    {
        var jsonContent = "{\"results\":[]}";
        var fakeResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonContent)
        };
        var controller = CreateController(fakeResponse);

        var result = await controller.Search("cats");

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", contentResult.ContentType);
        Assert.Equal(jsonContent, contentResult.Content);
    }
}
