using System.Net;
using System.Net.Http.Json;
using TaskTracker.Api.Tasks;

namespace TaskTracker.Api.Tests;

internal static class Helper
{
    public static async Task<TaskResponse> CreateTaskAsync(HttpClient client, string title = "write tests")
    {
        var response = await client.PostAsJsonAsync("/tasks", new CreateTaskRequest(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(created);
        return created;
    }

    public static async Task<TaskResponse> UpdateAndFetchAsync(HttpClient client, Guid id, UpdateTaskRequest update)
    {
        var put = await client.PutAsJsonAsync($"/tasks/{id}", update);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var fetched = await client.GetFromJsonAsync<TaskResponse>($"/tasks/{id}");
        Assert.NotNull(fetched);
        return fetched;
    }
}