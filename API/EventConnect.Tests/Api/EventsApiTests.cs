using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventConnect.Tests.Support;

namespace EventConnect.Tests.Api;

/// <summary>The HTTP contract in docs/requirements/02-events.md: routes, status codes and body shapes.</summary>
public sealed class EventsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct-horse-42";

    private static readonly string[] EventProperties =
    [
        "id", "status", "title", "description", "categoryId", "format", "startsAt", "endsAt", "timeZone", "venueName",
        "address", "virtualUrl", "rsvpDeadline", "capacity", "allowPlusOnes", "defaultMaxPlusOnes", "visibility",
        "publishedAt", "cancelledAt", "archivedAt", "createdAt", "updatedAt"
    ];

    /// <summary>The sample event from the requirements, moved to 2030 so it stays in the future.</summary>
    private static readonly object SampleEvent = new
    {
        title = "Maya's 30th Birthday",
        description = "Dinner and drinks on the rooftop.",
        categoryId = 1,
        format = "Physical",
        startsAt = "2030-12-05T19:00:00",
        endsAt = "2030-12-05T23:00:00",
        timeZone = "America/New_York",
        venueName = "Skyline Rooftop",
        address = "123 Main St, New York, NY 10001",
        virtualUrl = (string?)null,
        rsvpDeadline = "2030-11-28T23:59:00",
        capacity = 40,
        allowPlusOnes = true,
        defaultMaxPlusOnes = 1,
        visibility = "Private"
    };

    // GET /api/categories

    [Fact]
    public async Task Categories_are_listed_without_sign_in()
    {
        var response = await factory.CreateClient().GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = (await ReadJsonAsync(response)).EnumerateArray().First();
        Assert.Equal("""{"id":1,"name":"Birthday","slug":"birthday"}""", first.GetRawText());
    }

    // POST /api/events

    [Fact]
    public async Task Create_with_only_a_title_returns_201_with_the_sample_shape()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/events", new { title = "Maya's 30th Birthday" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        AssertProperties(body, EventProperties);
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal("UTC", body.GetProperty("timeZone").GetString()); // the profile's zone
        Assert.Equal("Private", body.GetProperty("visibility").GetString());
        Assert.EndsWith("Z", body.GetProperty("createdAt").GetString());
        Assert.Equal($"/api/events/{body.GetProperty("id").GetInt32()}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Create_returns_local_times_as_sent_without_an_offset()
    {
        var client = await SignedInClientAsync();

        var body = await ReadJsonAsync(await client.PostAsJsonAsync("/api/events", SampleEvent));

        Assert.Equal("2030-12-05T19:00:00", body.GetProperty("startsAt").GetString());
        Assert.Equal("Physical", body.GetProperty("format").GetString());
    }

    [Theory]
    [InlineData("""{ "description": "no title" }""", "Title")]
    [InlineData("""{ "title": "Party", "virtualUrl": "not a url" }""", "VirtualUrl")]
    [InlineData("""{ "title": "Party", "timeZone": "Mars/Base" }""", "TimeZone")]
    [InlineData("""{ "title": "Party", "capacity": 0 }""", "Capacity")]
    [InlineData("""{ "title": "Party", "defaultMaxPlusOnes": 11 }""", "DefaultMaxPlusOnes")]
    [InlineData("""{ "title": "Party", "startsAt": "2030-12-05T19:00:00Z" }""", "StartsAt")]
    public async Task Create_with_a_bad_field_returns_400_naming_it(string json, string field)
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsync("/api/events", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        await AssertValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task Signed_out_requests_return_401()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/events", new { title = "Party" })).StatusCode);
    }

    // POST /api/events/{id}/publish

    [Fact]
    public async Task Publishing_a_title_only_draft_returns_400_listing_every_missing_field() // AC-01
    {
        var client = await SignedInClientAsync(verified: true);
        var id = await CreateAsync(client, new { title = "Party" });

        var response = await client.PostAsync($"/api/events/{id}/publish", null);

        await AssertValidationProblemAsync(response, "CategoryId");
        var problem = await ReadJsonAsync(response);
        Assert.Equal("Event is not ready to publish.", problem.GetProperty("title").GetString());
        Assert.Equal(["CategoryId", "Format", "StartsAt"], problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData("Physical", "address", "Address")]
    [InlineData("Virtual", "virtualUrl", "VirtualUrl")]
    public async Task An_event_missing_its_format_location_cannot_be_published(string format, string missing, string field)
    {
        var client = await SignedInClientAsync(verified: true);
        var fields = SampleFields();
        fields["format"] = format;
        fields["virtualUrl"] = "https://meet.example.com/maya";
        fields[missing] = null;
        var id = await CreateAsync(client, fields);

        await AssertValidationProblemAsync(await client.PostAsync($"/api/events/{id}/publish", null), field);
    }

    [Theory]
    [InlineData("endsAt", "2030-12-05T18:00:00", "EndsAt")]
    [InlineData("rsvpDeadline", "2030-12-06T00:00:00", "RsvpDeadline")]
    [InlineData("startsAt", "2020-12-05T19:00:00", "StartsAt")]
    public async Task Publishing_with_bad_dates_returns_400(string property, string value, string field)
    {
        var client = await SignedInClientAsync(verified: true);
        var fields = SampleFields();
        fields[property] = value;
        var id = await CreateAsync(client, fields);

        await AssertValidationProblemAsync(await client.PostAsync($"/api/events/{id}/publish", null), field);
    }

    [Fact]
    public async Task Publishing_a_complete_draft_returns_200_then_409()
    {
        var client = await SignedInClientAsync(verified: true);
        var id = await CreateAsync(client, SampleEvent);

        var response = await client.PostAsync($"/api/events/{id}/publish", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Published", (await ReadJsonAsync(response)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/events/{id}/publish", null)).StatusCode);
    }

    [Fact]
    public async Task An_unverified_organizer_gets_403_when_publishing()
    {
        var client = await SignedInClientAsync(verified: false);
        var id = await CreateAsync(client, SampleEvent);

        var response = await client.PostAsync($"/api/events/{id}/publish", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Verify your email address before publishing.", (await ReadJsonAsync(response)).GetProperty("title").GetString());
    }

    // PUT /api/events/{id}

    [Fact]
    public async Task Editing_a_published_event_with_notify_guests_returns_200()
    {
        var client = await SignedInClientAsync(verified: true);
        var id = await CreatePublishedAsync(client);
        var fields = SampleFields();
        fields["startsAt"] = "2030-12-05T20:00:00";
        fields["notifyGuests"] = true;
        fields["changeMessage"] = "We moved to 8pm, see you there!";

        var response = await client.PutAsJsonAsync($"/api/events/{id}", fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2030-12-05T20:00:00", (await ReadJsonAsync(response)).GetProperty("startsAt").GetString());
    }

    [Fact]
    public async Task Editing_a_published_event_into_an_invalid_state_returns_400()
    {
        var client = await SignedInClientAsync(verified: true);
        var id = await CreatePublishedAsync(client);
        var fields = SampleFields();
        fields["venueName"] = null;

        await AssertValidationProblemAsync(await client.PutAsJsonAsync($"/api/events/{id}", fields), "VenueName");
    }

    // POST /api/events/{id}/cancel

    [Fact]
    public async Task Cancelling_sets_cancelled_and_later_edits_return_409() // AC-07, event part
    {
        var client = await SignedInClientAsync(verified: true);
        var id = await CreatePublishedAsync(client);

        var response = await client.PostAsJsonAsync($"/api/events/{id}/cancel", new { message = "Sorry, we have to call this off because of the storm." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("cancelledAt").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/events/{id}", SampleEvent)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/events/{id}/publish", null)).StatusCode);
    }

    [Fact]
    public async Task Cancel_needs_no_body_but_rejects_a_long_message()
    {
        var client = await SignedInClientAsync(verified: true);
        var first = await CreatePublishedAsync(client);
        var second = await CreatePublishedAsync(client);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/events/{first}/cancel", null)).StatusCode);
        await AssertValidationProblemAsync(await client.PostAsJsonAsync($"/api/events/{second}/cancel", new { message = new string('x', 1001) }), "Message");
    }

    [Fact]
    public async Task Cancelling_a_draft_returns_409()
    {
        var client = await SignedInClientAsync();
        var id = await CreateAsync(client, new { title = "Party" });

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/events/{id}/cancel", null)).StatusCode);
    }

    // POST /api/events/{id}/archive and GET /api/events

    [Fact]
    public async Task Archived_events_are_left_out_of_the_list_unless_asked_for()
    {
        var client = await SignedInClientAsync();
        var kept = await CreateAsync(client, new { title = "Kept" });
        var archived = await CreateAsync(client, new { title = "Old" });

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/events/{archived}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/events/{archived}/archive", null)).StatusCode);

        Assert.Equal([kept], await ListIdsAsync(client, ""));
        Assert.Equal([archived], await ListIdsAsync(client, "?status=Archived"));
    }

    [Fact]
    public async Task List_sorts_by_each_events_own_time_zone_and_filters()
    {
        var client = await SignedInClientAsync();
        // 19:00 in Tokyo (10:00 UTC) is earlier than 19:00 in New York (00:00 UTC next day).
        var newYork = await CreateAsync(client, new { title = "New York party", startsAt = "2030-12-05T19:00:00", timeZone = "America/New_York" });
        var tokyo = await CreateAsync(client, new { title = "Tokyo party", startsAt = "2030-12-05T19:00:00", timeZone = "Asia/Tokyo" });
        var past = await CreateAsync(client, new { title = "Old dinner", startsAt = "2020-01-01T19:00:00" });

        Assert.Equal([past, tokyo, newYork], await ListIdsAsync(client, ""));
        Assert.Equal([tokyo, newYork], await ListIdsAsync(client, "?when=upcoming"));
        Assert.Equal([past], await ListIdsAsync(client, "?when=past"));
        Assert.Equal([newYork], await ListIdsAsync(client, "?search=york"));
    }

    // POST /api/events/{id}/duplicate and DELETE /api/events/{id}

    [Fact]
    public async Task Duplicate_returns_201_with_a_new_draft_and_location()
    {
        var client = await SignedInClientAsync(verified: true);
        var id = await CreatePublishedAsync(client);

        var response = await client.PostAsync($"/api/events/{id}/duplicate", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal("Maya's 30th Birthday (copy)", body.GetProperty("title").GetString());
        Assert.Equal("Skyline Rooftop", body.GetProperty("venueName").GetString());
        Assert.Equal($"/api/events/{body.GetProperty("id").GetInt32()}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Delete_removes_a_draft_with_204_and_refuses_a_published_event_with_409()
    {
        var client = await SignedInClientAsync(verified: true);
        var draft = await CreateAsync(client, new { title = "Test event" });
        var published = await CreatePublishedAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/events/{draft}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/events/{draft}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/events/{published}")).StatusCode);
    }

    // Ownership (FR-004)

    [Fact]
    public async Task Another_organizers_event_returns_404_on_every_endpoint()
    {
        var owner = await SignedInClientAsync(verified: true);
        var id = await CreateAsync(owner, SampleEvent);
        var other = await SignedInClientAsync(verified: true);

        HttpResponseMessage[] responses =
        [
            await other.GetAsync($"/api/events/{id}"),
            await other.PutAsJsonAsync($"/api/events/{id}", SampleEvent),
            await other.PostAsync($"/api/events/{id}/publish", null),
            await other.PostAsync($"/api/events/{id}/cancel", null),
            await other.PostAsync($"/api/events/{id}/archive", null),
            await other.PostAsync($"/api/events/{id}/duplicate", null),
            await other.DeleteAsync($"/api/events/{id}")
        ];

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Empty(await ListIdsAsync(other, ""));
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/events/{id}")).StatusCode);
    }

    private static Dictionary<string, object?> SampleFields() =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(SampleEvent))!;

    /// <summary>A client signed in as a new account; verified accounts have followed the emailed link.</summary>
    private async Task<HttpClient> SignedInClientAsync(bool verified = false)
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName = "Maria Lopez" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        if (verified)
        {
            var token = factory.Emails.LatestToken(email, "verify-email");
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode);
        }
        return client;
    }

    private static async Task<int> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/events", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    private static async Task<int> CreatePublishedAsync(HttpClient client)
    {
        var id = await CreateAsync(client, SampleEvent);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/events/{id}/publish", null)).StatusCode);
        return id;
    }

    private static async Task<int[]> ListIdsAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/events{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await ReadJsonAsync(response)).EnumerateArray().Select(e => e.GetProperty("id").GetInt32())];
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static void AssertProperties(JsonElement body, params string[] names) =>
        Assert.Equal(names.Order(), body.EnumerateObject().Select(p => p.Name).Order());

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"Expected errors.{field} in {errors}");
    }
}
