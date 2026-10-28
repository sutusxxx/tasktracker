using System.Net;
using System.Net.Http.Json;
using TaskTracker.Api.Tasks;

namespace TaskTracker.Api.Tests;

public sealed class TaskEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClientFor(Guid.NewGuid().ToString());

    private static async Task<TaskResponse> CreateTaskAsync(HttpClient client, string title = "write tests")
    {
        var response = await client.PostAsJsonAsync("/tasks", new CreateTaskRequest(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(created);
        return created;
    }

    private static async Task<TaskResponse> UpdateAndFetchAsync(HttpClient client, Guid id, UpdateTaskRequest update)
    {
        var put = await client.PutAsJsonAsync($"/tasks/{id}", update);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var fetched = await client.GetFromJsonAsync<TaskResponse>($"/tasks/{id}");
        Assert.NotNull(fetched);
        return fetched;
    }

    [Fact]
    public async Task CreateThenGetReturnsTheTask()
    {
        var created = await CreateTaskAsync(_client);
        var fetched = await _client.GetFromJsonAsync<TaskResponse>($"/tasks/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("write tests", fetched.Title);
        Assert.False(fetched.Done);
    }

    [Fact]
    public async Task ListContainsCreatedTask()
    {
        var created = await CreateTaskAsync(_client);
        var all = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks");

        Assert.NotNull(all);
        Assert.Contains(all.Items, t => t.Id == created.Id);
        Assert.Equal(1, all.TotalCount);
    }

    [Fact]
    public async Task ListReturnsOnlyTheCallersTasks()
    {  
        await CreateTaskAsync(_client, "owned");
        await CreateTaskAsync(factory.CreateClientFor(Guid.NewGuid().ToString()), "stranger");
        var fetched = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks");
        
        Assert.NotNull(fetched);
        Assert.Single(fetched.Items);
        Assert.Equal("owned", fetched.Items[0].Title);
        Assert.Equal(1, fetched.TotalCount);
    }

    [Fact]
    public async Task ListReturnsFilteredTasksByTitle()
    {
        var created = new List<TaskResponse>();
        foreach (var title in new [] { "first", "second with spaces", "third contains test", "fourth test" })
        {
            created.Add(await CreateTaskAsync(_client, title));
        }

        var filterFirst = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?search=first");
        Assert.NotNull(filterFirst);
        Assert.Single(filterFirst.Items);
        Assert.Equal(created[0].Id, filterFirst.Items[0].Id);
        
        var filterSecond = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>($"/tasks?search=second{Uri.EscapeDataString(" ")}with");
        Assert.NotNull(filterSecond);
        Assert.Single(filterSecond.Items);
        Assert.Equal(created[1].Id, filterSecond.Items[0].Id);
        
        var filterThird = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?search=contains");
        Assert.NotNull(filterThird);
        Assert.Single(filterThird.Items);
        Assert.Equal(created[2].Id, filterThird.Items[0].Id);
        
        var filterMultiple = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?search=test");
        Assert.NotNull(filterMultiple);
        Assert.Equal(2, filterMultiple.TotalCount);
        Assert.Equal(2, filterMultiple.Items.Count);
        Assert.Equal(created.Skip(2).Select(t => t.Id), filterMultiple.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task ListReturnsFilteredTasksByDone()
    {
        var created = new List<TaskResponse>();
        foreach (var title in new [] { "first", "second" })
        {
            created.Add(await CreateTaskAsync(_client, title));
        }

        var updated = await UpdateAndFetchAsync(_client, created[0].Id, new UpdateTaskRequest("first done", true));
        
        var filterDone = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?done=true");
        Assert.NotNull(filterDone);
        Assert.Single(filterDone.Items);
        Assert.Equal(updated.Id, filterDone.Items[0].Id);
        
        var filterNotDone = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?done=false");
        Assert.NotNull(filterNotDone);
        Assert.Single(filterNotDone.Items);
        Assert.Equal(created[1].Id, filterNotDone.Items[0].Id);
    }

    [Fact]
    public async Task ListPagesThroughAllTasksInCreationOrder()
    {
        var created = new List<TaskResponse>();
        foreach (var title in new[] { "one", "two", "three", "four", "five" })
            created.Add(await CreateTaskAsync(_client, title));

        var pageOne = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?page=1&pageSize=3");
        var pageTwo = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?page=2&pageSize=3");

        Assert.NotNull(pageOne);
        Assert.NotNull(pageTwo);
        Assert.Equal(5, pageOne.TotalCount);
        Assert.Equal(created.Take(3).Select(t => t.Id), pageOne.Items.Select(t => t.Id));
        Assert.Equal(created.Skip(3).Select(t => t.Id), pageTwo.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task ListReturnsEmptyWhenPageIsTooBig()
    {
        foreach (var title in new[] { "one", "two", "three", "four", "five" })
            await CreateTaskAsync(_client, title);
        
        var page100 = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?page=100");
        Assert.NotNull(page100);
        Assert.Empty(page100.Items);
        Assert.Equal(5, page100.TotalCount);
    }
    
    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("sort=bogus")]
    public async Task ListWithInvalidQueryReturns400(string query)
    {
        var response = await _client.GetAsync($"/tasks?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        var created = await CreateTaskAsync(_client);
        var fetched = await UpdateAndFetchAsync(_client, created.Id, new UpdateTaskRequest("updated", true));
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
        var created = await CreateTaskAsync(_client);

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

    [Fact]
    public async Task GetWithoutAuthHeaderReturns401()
    {
        var noAuthClient = factory.CreateClient();
        var response = await noAuthClient.GetAsync("/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskForDifferentUserReturns404()
    {
        var created = await CreateTaskAsync(_client);
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
        var first = await CreateTaskAsync(factory.CreateClientFor(Guid.NewGuid().ToString()));
        var second = await CreateTaskAsync(factory.CreateClientFor(Guid.NewGuid().ToString()));
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
}