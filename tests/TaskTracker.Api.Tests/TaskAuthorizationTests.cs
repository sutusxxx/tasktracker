using System.Net;
using System.Net.Http.Json;
using TaskTracker.Api.Tasks;

namespace TaskTracker.Api.Tests;

[Collection("api")]
public sealed class TaskAuthorizationTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClientFor(Guid.NewGuid().ToString());

    
    [Fact]
    public async Task GetTaskForDifferentUserReturns404()
    {
        var created = await Helper.CreateTaskAsync(_client);
        var newClient = factory.CreateClientFor(Guid.NewGuid().ToString());
        
        var responseGet =  await newClient.GetAsync($"/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, responseGet.StatusCode);
        
        var responsePut =  await newClient.PutAsJsonAsync($"/tasks/{created.Id}", new UpdateTaskRequest("new title", false));
        Assert.Equal(HttpStatusCode.NotFound, responsePut.StatusCode);
        
        var responseDelete = await newClient.DeleteAsync($"/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, responseDelete.StatusCode);
    }

    [Fact]
    public async Task AdminListReturnsTasksOfAllUsers()
    {
        var first = await Helper.CreateTaskAsync(factory.CreateClientFor(Guid.NewGuid().ToString()));
        var second = await Helper.CreateTaskAsync(factory.CreateClientFor(Guid.NewGuid().ToString()));
        var adminClient = factory.CreateClientFor(Guid.NewGuid().ToString(), "admin");
        
        var fetched = await adminClient.GetFromJsonAsync<List<TaskResponse>>($"/admin/tasks");
        
        Assert.NotNull(fetched);
        Assert.Contains(fetched, t => t.Id == first.Id);
        Assert.Contains(fetched, t => t.Id == second.Id);
    }

    [Fact]
    public async Task AdminListReturns403ForNonAdminUser()
    {
        var response = await _client.GetAsync("/admin/tasks");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWithoutAuthHeaderReturns401()
    {
        var noAuthClient = factory.CreateClient();
        var response = await noAuthClient.GetAsync("/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}