using System.Net;
using System.Net.Http.Json;
using TaskTracker.Api.Tasks;

namespace TaskTracker.Api.Tests;

[Collection("api")]
public sealed class TaskCrudTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClientFor(Guid.NewGuid().ToString());

    [Fact]
    public async Task CreateThenGetReturnsTheTask()
    {
        var created = await Helper.CreateTaskAsync(_client);
        var fetched = await _client.GetFromJsonAsync<TaskResponse>($"/tasks/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("write tests", fetched.Title);
        Assert.False(fetched.Done);
    }

    [Fact]
    public async Task GetUnknownIdReturns404()
    {
        var response = await _client.GetAsync($"/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithEmptyTitleReturns400ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new CreateTaskRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdateChangesAreVisibleOnGet()
    {
        var created = await Helper.CreateTaskAsync(_client);
        var fetched = await Helper.UpdateAndFetchAsync(_client, created.Id, new UpdateTaskRequest("updated", true));
        Assert.Equal("updated", fetched.Title);
        Assert.True(fetched.Done);
    }

    [Fact]
    public async Task UpdateUnknownIdReturns404()
    {
        var response = await _client.PutAsJsonAsync($"/tasks/{Guid.NewGuid()}", new UpdateTaskRequest("x", Done: true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRemovesTheTask()
    {
        var created = await Helper.CreateTaskAsync(_client);

        var deleted = await _client.DeleteAsync($"/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var fetched = await _client.GetAsync($"/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
    }

    [Fact]
    public async Task DeleteUnknownIdReturns404()
    {
        var response = await _client.DeleteAsync($"/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
   
}