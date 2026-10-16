
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RaceDayContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RaceDay")));

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSession();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

static int? UserId(HttpContext c) =>
    int.TryParse(c.Session.GetString("UserId"), out var id) ? id : null;

static string? Role(HttpContext c) =>
    c.Session.GetString("Role");

static bool IsRole(HttpContext c, string role) =>
    UserId(c) is not null && Role(c) == role;

static IResult ForbiddenOrUnauthorized(HttpContext c) =>
    UserId(c) is null ? Results.Unauthorized() : Results.StatusCode(403);

// AUTHENTICATION
var auth = app.MapGroup("/api/auth");

auth.MapPost("/register", async (
    RegisterRequest r,
    RaceDayContext db,
    IPasswordHasher<AppUser> hasher) =>
{
    if (string.IsNullOrWhiteSpace(r.FirstName) ||
        string.IsNullOrWhiteSpace(r.LastName) ||
        string.IsNullOrWhiteSpace(r.Email) ||
        !r.Email.Contains('@') ||
        string.IsNullOrWhiteSpace(r.Password) ||
        r.Password.Length < 8 ||
        (r.Role != "Participant" && r.Role != "Organiser"))
    {
        return Results.BadRequest(
            "Provide valid names, email, a password of at least 8 characters, and a valid role.");
    }

    var email = r.Email.Trim().ToLowerInvariant();

    if (await db.Users.AnyAsync(x => x.Email == email))
        return Results.Conflict("Email already registered.");

    var u = new AppUser
    {
        FirstName = r.FirstName.Trim(),
        LastName = r.LastName.Trim(),
        Email = email,
        Role = r.Role
    };

    u.PasswordHash = hasher.HashPassword(u, r.Password);
    u.Profile = new UserProfile();

    db.Users.Add(u);
    await db.SaveChangesAsync();

    return Results.Created($"/api/users/{u.UserId}",
        new { u.UserId, u.FirstName, u.LastName, u.Email, u.Role });
})
.WithSummary("Register a participant or organiser.");

auth.MapPost("/login", async (
    LoginRequest r,
    HttpContext c,
    RaceDayContext db,
    IPasswordHasher<AppUser> hasher) =>
{
    if (string.IsNullOrWhiteSpace(r.Email) ||
        string.IsNullOrWhiteSpace(r.Password))
        return Results.BadRequest("Email and password are required.");

    var email = r.Email.Trim().ToLowerInvariant();

    var u = await db.Users.SingleOrDefaultAsync(x => x.Email == email);

    if (u is null ||
        hasher.VerifyHashedPassword(u, u.PasswordHash, r.Password)
        == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    c.Session.SetString("UserId", u.UserId.ToString());
    c.Session.SetString("Role", u.Role);

    return Results.Ok(new
    {
        message = "Login successful",
        user = new { u.UserId, u.FirstName, u.LastName, u.Email, u.Role },
        authentication = "Session cookie"
    });
})
.WithSummary("Log in and establish an authenticated session.");

auth.MapPost("/logout", (HttpContext c) =>
{
    c.Session.Clear();
    return Results.NoContent();
})
.WithSummary("End the current session.");

// USERS AND PROFILES
var users = app.MapGroup("/api/users");

users.MapGet("/me", async (HttpContext c, RaceDayContext db) =>
{
    var id = UserId(c);
    if (id is null) return Results.Unauthorized();

    var u = await db.Users
        .Include(x => x.Profile)
        .Where(x => x.UserId == id.Value)
        .Select(x => new
        {
            x.UserId,
            x.FirstName,
            x.LastName,
            x.Email,
            x.Role,
            x.Profile
        })
        .FirstOrDefaultAsync();

    return u is null ? Results.NotFound() : Results.Ok(u);
})
.WithSummary("Get the current user's profile.");

users.MapPut("/me", async (
    ProfileUpdate r, HttpContext c, RaceDayContext db) =>
{
    var id = UserId(c);
    if (id is null) return Results.Unauthorized();

    var u = await db.Users
        .Include(x => x.Profile)
        .FirstOrDefaultAsync(x => x.UserId == id.Value);

    if (u is null) return Results.NotFound();

    if (r.FirstName is not null) u.FirstName = r.FirstName.Trim();
    if (r.LastName is not null) u.LastName = r.LastName.Trim();

    u.Profile ??= new UserProfile { UserId = u.UserId };
    u.Profile.Phone = r.Phone;
    u.Profile.DateOfBirth = r.DateOfBirth;
    u.Profile.EmergencyContactName = r.EmergencyContactName;
    u.Profile.EmergencyContactPhone = r.EmergencyContactPhone;
    u.Profile.City = r.City;
    u.Profile.Province = r.Province;

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        u.UserId,
        u.FirstName,
        u.LastName,
        u.Email,
        u.Role,
        u.Profile
    });
})
.WithSummary("Update the current user's profile.");

users.MapGet("/{id:int}", async (
    int id, HttpContext c, RaceDayContext db) =>
{
    if (UserId(c) is null) return Results.Unauthorized();

    var u = await db.Users
        .Where(x => x.UserId == id)
        .Select(x => new
        {
            x.UserId,
            x.FirstName,
            x.LastName,
            x.Profile
        })
        .FirstOrDefaultAsync();

    return u is null ? Results.NotFound() : Results.Ok(u);
})
.WithSummary("Get a public profile by user ID.");

// EVENTS
var events = app.MapGroup("/api/events");

events.MapGet("/", async (
    RaceDayContext db,
    string? eventType,
    string? city,
    string? province,
    DateOnly? dateFrom,
    DateOnly? dateTo) =>
{
    var q = db.Events.Include(x => x.Categories).AsQueryable();

    if (!string.IsNullOrWhiteSpace(eventType))
        q = q.Where(x => x.EventType == eventType);

    if (!string.IsNullOrWhiteSpace(city))
        q = q.Where(x => x.City.Contains(city));

    if (!string.IsNullOrWhiteSpace(province))
        q = q.Where(x => x.Province.Contains(province));

    if (dateFrom.HasValue)
        q = q.Where(x => x.EventDate >= dateFrom.Value);

    if (dateTo.HasValue)
        q = q.Where(x => x.EventDate <= dateTo.Value);

    return Results.Ok(await q.OrderBy(x => x.EventDate).ToListAsync());
})
.WithSummary("List events with optional filters.");

events.MapGet("/{id:int}", async (int id, RaceDayContext db) =>
{
    var e = await db.Events
        .Include(x => x.Categories)
        .FirstOrDefaultAsync(x => x.EventId == id);

    return e is null ? Results.NotFound() : Results.Ok(e);
})
.WithSummary("Get event details and categories.");

events.MapPost("/", async (
    EventRequest r, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    if (r.EventType is not ("Running" or "Walking" or "Cycling"))
        return Results.BadRequest(
            "EventType must be Running, Walking or Cycling.");

    var e = new RaceEvent
    {
        OrganiserId = UserId(c)!.Value,
        EventName = r.EventName,
        EventType = r.EventType,
        Description = r.Description,
        EventDate = r.EventDate,
        StartTime = r.StartTime,
        Venue = r.Venue,
        City = r.City,
        Province = r.Province,
        Status = r.Status
    };

    db.Events.Add(e);
    await db.SaveChangesAsync();

    return Results.Created($"/api/events/{e.EventId}", e);
})
.WithSummary("Create an event as an organiser.");

events.MapPut("/{id:int}", async (
    int id, EventRequest r, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var e = await db.Events.FirstOrDefaultAsync(x => x.EventId == id);
    if (e is null) return Results.NotFound();
    if (e.OrganiserId != UserId(c)) return Results.StatusCode(403);

    e.EventName = r.EventName;
    e.EventType = r.EventType;
    e.Description = r.Description;
    e.EventDate = r.EventDate;
    e.StartTime = r.StartTime;
    e.Venue = r.Venue;
    e.City = r.City;
    e.Province = r.Province;
    e.Status = r.Status;

    await db.SaveChangesAsync();
    return Results.Ok(e);
})
.WithSummary("Update an event owned by the signed-in organiser.");

events.MapDelete("/{id:int}", async (
    int id, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var e = await db.Events
        .Include(x => x.Categories)
        .ThenInclude(x => x.Enrolments)
        .FirstOrDefaultAsync(x => x.EventId == id);

    if (e is null) return Results.NotFound();
    if (e.OrganiserId != UserId(c)) return Results.StatusCode(403);

    if (e.Categories.Any(x => x.Enrolments.Any()))
    {
        e.Status = "Cancelled";
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    db.Events.Remove(e);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithSummary("Cancel or delete an event owned by the organiser.");

events.MapGet("/{eventId:int}/categories", async (
    int eventId, RaceDayContext db) =>
    await db.Categories.Where(x => x.EventId == eventId).ToListAsync())
.WithSummary("List categories for an event.");

events.MapPost("/{eventId:int}/categories", async (
    int eventId, CategoryRequest r, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var e = await db.Events.FirstOrDefaultAsync(x => x.EventId == eventId);
    if (e is null) return Results.NotFound();
    if (e.OrganiserId != UserId(c)) return Results.StatusCode(403);

    if (r.DistanceKm <= 0 || r.EntryFee < 0 ||
        r.MaximumParticipants <= 0)
        return Results.BadRequest(
            "Distance must be positive; fee cannot be negative; maximum participants must be positive.");

    var cat = new RaceCategory
    {
        EventId = eventId,
        CategoryName = r.CategoryName,
        DistanceKm = r.DistanceKm,
        EntryFee = r.EntryFee,
        MaximumParticipants = r.MaximumParticipants
    };

    db.Categories.Add(cat);
    await db.SaveChangesAsync();

    return Results.Created($"/api/categories/{cat.CategoryId}", cat);
})
.WithSummary("Add a category to an event.");

events.MapGet("/{eventId:int}/enrolments", async (
    int eventId, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var e = await db.Events.FirstOrDefaultAsync(x => x.EventId == eventId);
    if (e is null) return Results.NotFound();
    if (e.OrganiserId != UserId(c)) return Results.StatusCode(403);

    var enrolments = await db.Enrolments
        .Include(x => x.Participant)
        .Include(x => x.Category)
        .Where(x => x.Category!.EventId == eventId)
        .ToListAsync();

    return Results.Ok(enrolments);
})
.WithSummary("List enrolments for an event owned by the organiser.");

// ENROL IN AN EVENT CATEGORY
events.MapPost("/{eventId:int}/enrolments", async (
    int eventId, EnrolmentRequest r, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Participant"))
        return ForbiddenOrUnauthorized(c);

    int participantId = UserId(c)!.Value;

    var cat = await db.Categories
        .Include(x => x.Event)
        .FirstOrDefaultAsync(x =>
            x.CategoryId == r.CategoryId && x.EventId == eventId);

    if (cat is null)
        return Results.BadRequest("Category does not belong to this event.");

    if (cat.Event!.EventDate < DateOnly.FromDateTime(DateTime.UtcNow))
        return Results.BadRequest("This event has already taken place.");

    if (await db.Enrolments.AnyAsync(x =>
        x.ParticipantId == participantId && x.CategoryId == cat.CategoryId))
        return Results.Conflict("Already enrolled in this category.");

    var count = await db.Enrolments.CountAsync(x =>
        x.CategoryId == cat.CategoryId && x.EnrolmentStatus == "Active");

    if (cat.MaximumParticipants is not null &&
        count >= cat.MaximumParticipants)
        return Results.Conflict("Category is full.");

    var en = new Enrolment
    {
        ParticipantId = participantId,
        CategoryId = cat.CategoryId
    };

    db.Enrolments.Add(en);
    await db.SaveChangesAsync();

    return Results.Created($"/api/enrolments/{en.EnrolmentId}", en);
})
.WithSummary("Enrol the signed-in participant in a category.");

events.MapGet("/{eventId:int}/results", async (
    int eventId, RaceDayContext db) =>
{
    var exists = await db.Events.AnyAsync(x => x.EventId == eventId);
    if (!exists) return Results.NotFound();

    var results = await db.Results
        .Include(x => x.Enrolment)
        .ThenInclude(x => x!.Category)
        .Include(x => x.Enrolment)
        .ThenInclude(x => x!.Participant)
        .Where(x => x.Enrolment!.Category!.EventId == eventId)
        .ToListAsync();

    return Results.Ok(results);
})
.WithSummary("View results for an event.");

events.MapPost("/{eventId:int}/results", async (
    int eventId, ResultRequest r, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var e = await db.Events.FirstOrDefaultAsync(x => x.EventId == eventId);
    if (e is null) return Results.NotFound();
    if (e.OrganiserId != UserId(c)) return Results.StatusCode(403);

    var en = await db.Enrolments
        .Include(x => x.Category)
        .FirstOrDefaultAsync(x =>
            x.EnrolmentId == r.EnrolmentId &&
            x.Category!.EventId == eventId);

    if (en is null)
        return Results.BadRequest("Enrolment does not belong to this event.");

    if (await db.Results.AnyAsync(x => x.EnrolmentId == r.EnrolmentId))
        return Results.Conflict("A result already exists for this enrolment.");

    if (r.FinishPosition <= 0 || r.FinishTimeSeconds <= 0)
        return Results.BadRequest("Position and time must be positive.");

    var result = new RaceResult
    {
        EnrolmentId = r.EnrolmentId,
        FinishPosition = r.FinishPosition,
        FinishTimeSeconds = r.FinishTimeSeconds,
        ResultStatus = r.ResultStatus
    };

    db.Results.Add(result);
    await db.SaveChangesAsync();

    return Results.Created($"/api/results/{result.ResultId}", result);
})
.WithSummary("Record a result for an enrolment.");

// CATEGORIES
var categories = app.MapGroup("/api/categories");

categories.MapPut("/{id:int}", async (
    int id, CategoryRequest r, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var cat = await db.Categories
        .Include(x => x.Event)
        .FirstOrDefaultAsync(x => x.CategoryId == id);

    if (cat is null) return Results.NotFound();
    if (cat.Event!.OrganiserId != UserId(c)) return Results.StatusCode(403);

    if (r.DistanceKm <= 0 || r.EntryFee < 0 ||
        r.MaximumParticipants <= 0)
        return Results.BadRequest("Invalid category values.");

    cat.CategoryName = r.CategoryName;
    cat.DistanceKm = r.DistanceKm;
    cat.EntryFee = r.EntryFee;
    cat.MaximumParticipants = r.MaximumParticipants;

    await db.SaveChangesAsync();
    return Results.Ok(cat);
})
.WithSummary("Update a category owned by the organiser.");

categories.MapDelete("/{id:int}", async (
    int id, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var cat = await db.Categories
        .Include(x => x.Event)
        .Include(x => x.Enrolments)
        .FirstOrDefaultAsync(x => x.CategoryId == id);

    if (cat is null) return Results.NotFound();
    if (cat.Event!.OrganiserId != UserId(c)) return Results.StatusCode(403);

    if (cat.Enrolments.Count > 0)
        return Results.Conflict("Cannot delete a category with enrolments.");

    db.Categories.Remove(cat);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithSummary("Delete a category if no enrolments exist.");

// ENROLMENTS
var enrolments = app.MapGroup("/api/enrolments");

enrolments.MapPut("/{id:int}", async (
    int id, EnrolmentUpdate r, HttpContext c, RaceDayContext db) =>
{
    var currentUserId = UserId(c);
    if (currentUserId is null) return Results.Unauthorized();

    var en = await db.Enrolments
        .Include(x => x.Category)
        .ThenInclude(x => x!.Event)
        .FirstOrDefaultAsync(x => x.EnrolmentId == id);

    if (en is null) return Results.NotFound();

    var owns = en.ParticipantId == currentUserId.Value;
    var organiserOwns = Role(c) == "Organiser" &&
        en.Category!.Event!.OrganiserId == currentUserId.Value;

    if (!owns && !organiserOwns) return Results.StatusCode(403);
    if (Role(c) == "Participant" && r.PaymentStatus is not null)
        return Results.StatusCode(403);

    if (r.PaymentStatus is not null &&
        new[] { "Pending", "Paid", "Refunded", "Waived" }.Contains(r.PaymentStatus))
        en.PaymentStatus = r.PaymentStatus;

    if (r.EnrolmentStatus is not null &&
        new[] { "Active", "Cancelled", "Completed" }.Contains(r.EnrolmentStatus))
        en.EnrolmentStatus = r.EnrolmentStatus;

    await db.SaveChangesAsync();
    return Results.Ok(en);
})
.WithSummary("Update permitted enrolment/payment status.");

enrolments.MapDelete("/{id:int}", async (
    int id, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Participant"))
        return ForbiddenOrUnauthorized(c);

    var en = await db.Enrolments.FirstOrDefaultAsync(x => x.EnrolmentId == id);
    if (en is null) return Results.NotFound();
    if (en.ParticipantId != UserId(c)) return Results.StatusCode(403);

    en.EnrolmentStatus = "Cancelled";
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithSummary("Cancel your own enrolment.");

// CURRENT PARTICIPANT'S ENROLMENTS
users.MapGet("/me/enrolments", async (
    HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Participant"))
        return ForbiddenOrUnauthorized(c);

    int participantId = UserId(c)!.Value;

    var participantEnrolments = await db.Enrolments
        .Include(x => x.Category)
        .ThenInclude(x => x!.Event)
        .Where(x => x.ParticipantId == participantId)
        .ToListAsync();

    return Results.Ok(participantEnrolments);
})
.WithSummary("List the current participant's enrolments.");

// CURRENT PARTICIPANT'S RESULTS
users.MapGet("/me/results", async (
    HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Participant"))
        return ForbiddenOrUnauthorized(c);

    int participantId = UserId(c)!.Value;

    var participantResults = await db.Results
        .Include(x => x.Enrolment)
        .ThenInclude(x => x!.Category)
        .ThenInclude(x => x!.Event)
        .Where(x => x.Enrolment!.ParticipantId == participantId)
        .ToListAsync();

    return Results.Ok(participantResults);
})
.WithSummary("View the current participant's results.");

// RESULTS
var results = app.MapGroup("/api/results");

results.MapPut("/{id:int}", async (
    int id, ResultUpdate r, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var result = await db.Results
        .Include(x => x.Enrolment)
        .ThenInclude(x => x!.Category)
        .ThenInclude(x => x!.Event)
        .FirstOrDefaultAsync(x => x.ResultId == id);

    if (result is null) return Results.NotFound();
    if (result.Enrolment!.Category!.Event!.OrganiserId != UserId(c))
        return Results.StatusCode(403);

    if (r.FinishPosition <= 0 || r.FinishTimeSeconds <= 0)
        return Results.BadRequest("Position and time must be positive.");

    result.FinishPosition = r.FinishPosition;
    result.FinishTimeSeconds = r.FinishTimeSeconds;
    result.ResultStatus = r.ResultStatus;

    await db.SaveChangesAsync();
    return Results.Ok(result);
})
.WithSummary("Correct a recorded result.");

results.MapDelete("/{id:int}", async (
    int id, HttpContext c, RaceDayContext db) =>
{
    if (!IsRole(c, "Organiser"))
        return ForbiddenOrUnauthorized(c);

    var result = await db.Results
        .Include(x => x.Enrolment)
        .ThenInclude(x => x!.Category)
        .ThenInclude(x => x!.Event)
        .FirstOrDefaultAsync(x => x.ResultId == id);

    if (result is null) return Results.NotFound();
    if (result.Enrolment!.Category!.Event!.OrganiserId != UserId(c))
        return Results.StatusCode(403);

    db.Results.Remove(result);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithSummary("Delete an incorrect result.");

// WEATHER ENDPOINT
app.MapGet("/api/events/{eventId:int}/weather", async (
    int eventId, RaceDayContext db) =>
{
    var exists = await db.Events.AnyAsync(x => x.EventId == eventId);

    if (!exists) return Results.NotFound();

    return Results.Problem(
        "Configure a weather provider API key to enable live forecasts.",
        statusCode: 502);
})
.WithSummary("Weather endpoint placeholder.");

// ROUTE INFORMATION ENDPOINT
app.MapGet("/api/events/{eventId:int}/route", async (
    int eventId, RaceDayContext db) =>
{
    var e = await db.Events
        .Where(x => x.EventId == eventId)
        .Select(x => new
        {
            x.EventId,
            x.EventName,
            x.Venue,
            x.City,
            x.Province
        })
        .FirstOrDefaultAsync();

    if (e is null) return Results.NotFound();

    return Results.Ok(new
    {
        eventInfo = e,
        routeInformation =
            "Route/map data must be supplied by the event organiser."
    });
})
.WithSummary("Return event location and route information placeholder.");

app.Run();
