using System.Net;
using System.Net.Http.Json;
using TaskTracker.Api.Tasks;

namespace TaskTracker.Api.Tests;

[Collection("api")]
public class TaskQueryTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClientFor(Guid.NewGuid().ToString());
    
    [Fact]
    public async Task ListContainsCreatedTask()
    {
        var created = await Helper.CreateTaskAsync(_client);
        var all = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks");

        Assert.NotNull(all);
        Assert.Contains(all.Items, t => t.Id == created.Id);
        Assert.Equal(1, all.TotalCount);
    }

    [Fact]
    public async Task ListReturnsOnlyTheCallersTasks()
    {  
        await Helper.CreateTaskAsync(_client, "owned");
        await Helper.CreateTaskAsync(factory.CreateClientFor(Guid.NewGuid().ToString()), "stranger");
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
            created.Add(await Helper.CreateTaskAsync(_client, title));
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
            created.Add(await Helper.CreateTaskAsync(_client, title));
        }

        var updated = await Helper.UpdateAndFetchAsync(_client, created[0].Id, new UpdateTaskRequest("first done", true));
        
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
            created.Add(await Helper.CreateTaskAsync(_client, title));

        var pageOne = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?page=1&pageSize=3");
        var pageTwo = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?page=2&pageSize=3");

        Assert.NotNull(pageOne);
        Assert.NotNull(pageTwo);
        Assert.Equal(5, pageOne.TotalCount);
        Assert.Equal(1, pageOne.Page);
        Assert.Equal(3, pageOne.PageSize);
        Assert.Equal(2, pageTwo.Page);
        Assert.Equal(3, pageTwo.PageSize);
        Assert.Equal(created.Take(3).Select(t => t.Id), pageOne.Items.Select(t => t.Id));
        Assert.Equal(created.Skip(3).Select(t => t.Id), pageTwo.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task ListReturnsEmptyWhenPageIsTooBig()
    {
        foreach (var title in new[] { "one", "two", "three", "four", "five" })
            await Helper.CreateTaskAsync(_client, title);
        
        var page100 = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>("/tasks?page=100");
        
        Assert.NotNull(page100);
        Assert.Empty(page100.Items);
        Assert.Equal(5, page100.TotalCount);
    }

    [Fact]
    public async Task ListReturnsSortedItemsOnPageWithSameTitles()
    {
        var created = new List<TaskResponse>();
        foreach (var title in new[] { "test", "test", "test" })
            created.Add(await Helper.CreateTaskAsync(_client, title));
        
        var pageOne = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>($"/tasks?page=1&pageSize=1&sort={TaskSortField.Title}");
        var pageTwo = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>($"/tasks?page=2&pageSize=1&ort={TaskSortField.Title}");
        var pageThree = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>($"/tasks?page=3&pageSize=1&sort={TaskSortField.Title}");
        
        Assert.NotNull(pageOne);
        Assert.NotNull(pageTwo);
        Assert.NotNull(pageThree);
        Assert.Equal(3, pageOne.TotalCount);
        Assert.Single(pageOne.Items);
        Assert.Equal(created[0].Id, pageOne.Items[0].Id);
        Assert.Equal(3, pageTwo.TotalCount);
        Assert.Single(pageTwo.Items);
        Assert.Equal(created[1].Id, pageTwo.Items[0].Id);
        Assert.Equal(3, pageThree.TotalCount);
        Assert.Single(pageThree.Items);
        Assert.Equal(created[2].Id, pageThree.Items[0].Id);
    }

    [Fact]
    public async Task ListReturnsItemsInAlphabeticalOrder()
    {
        var created = new List<TaskResponse>();
        foreach (var title in new[] { "b", "a", "c" })
            created.Add(await Helper.CreateTaskAsync(_client, title));
        
        var ascending = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>($"/tasks?page=1&pageSize=3&sort={TaskSortField.Title}");
        
        Assert.NotNull(ascending);
        Assert.Equal(3, ascending.TotalCount);
        Assert.Equal(
            created.OrderBy(t => t.Title).Select(t => t.Id), 
            ascending.Items.Select(t => t.Id));
        
        var descending = await _client.GetFromJsonAsync<PagedResponse<TaskResponse>>($"/tasks?page=1&pageSize=3&sort={TaskSortField.Title}&desc=true");
        Assert.NotNull(descending);
        Assert.Equal(3, descending.TotalCount);
        Assert.Equal(
            created.OrderByDescending(t => t.Title).Select(t => t.Id), 
            descending.Items.Select(t => t.Id));
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
}