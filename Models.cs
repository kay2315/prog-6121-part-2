using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RaceDay.Api;

public class AppUser
{
    public int UserId { get; set; }
    [Required, MaxLength(50)] public string FirstName { get; set; } = "";
    [Required, MaxLength(50)] public string LastName { get; set; } = "";
    [Required, MaxLength(255)] public string Email { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required] public string Role { get; set; } = "Participant";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserProfile? Profile { get; set; }
    public ICollection<RaceEvent> Events { get; set; } = new List<RaceEvent>();
}
public class UserProfile
{
    public int UserProfileId { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
}
public class RaceEvent
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public AppUser? Organiser { get; set; }
    [Required, MaxLength(150)] public string EventName { get; set; } = "";
    [Required] public string EventType { get; set; } = "Running";
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public string Venue { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string Status { get; set; } = "Upcoming";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RaceCategory> Categories { get; set; } = new List<RaceCategory>();
}
public class RaceCategory
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public RaceEvent? Event { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int? MaximumParticipants { get; set; }
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public AppUser? Participant { get; set; }
    public int CategoryId { get; set; }
    public RaceCategory? Category { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public string? RaceNumber { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string EnrolmentStatus { get; set; } = "Active";
    public RaceResult? Result { get; set; }
}
public class RaceResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }
    public int? FinishPosition { get; set; }
    public int? FinishTimeSeconds { get; set; }
    public string ResultStatus { get; set; } = "Finished";
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
public class RaceDayContext(DbContextOptions<RaceDayContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<RaceCategory> Categories => Set<RaceCategory>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<RaceResult> Results => Set<RaceResult>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().ToTable("Users").HasKey(x => x.UserId);
        b.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<UserProfile>().ToTable("UserProfiles").HasKey(x => x.UserProfileId);
        b.Entity<UserProfile>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<AppUser>().HasOne(x => x.Profile).WithOne(x => x.User).HasForeignKey<UserProfile>(x => x.UserId);
        b.Entity<RaceEvent>().ToTable("Events").HasKey(x => x.EventId);
        b.Entity<AppUser>().HasMany(x => x.Events).WithOne(x => x.Organiser).HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RaceCategory>().ToTable("Categories").HasKey(x => x.CategoryId);
        b.Entity<RaceCategory>().HasIndex(x => new { x.EventId, x.CategoryName }).IsUnique();
        b.Entity<RaceCategory>().Property(x => x.DistanceKm).HasPrecision(6,2);
        b.Entity<RaceCategory>().Property(x => x.EntryFee).HasPrecision(10,2);
        b.Entity<Enrolment>().ToTable("Enrolments").HasKey(x => x.EnrolmentId);
        b.Entity<Enrolment>().HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique();
        b.Entity<RaceResult>().ToTable("Results").HasKey(x => x.ResultId);
        b.Entity<RaceResult>().HasIndex(x => x.EnrolmentId).IsUnique();
    }
}
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role);
public record LoginRequest(string Email, string Password);
public record ProfileUpdate(string? FirstName, string? LastName, string? Phone, DateOnly? DateOfBirth, string? EmergencyContactName, string? EmergencyContactPhone, string? City, string? Province);
public record EventRequest(string EventName, string EventType, string? Description, DateOnly EventDate, TimeOnly StartTime, string Venue, string City, string Province, string Status);
public record CategoryRequest(string CategoryName, decimal DistanceKm, decimal EntryFee, int? MaximumParticipants);
public record EnrolmentRequest(int CategoryId);
public record EnrolmentUpdate(string? PaymentStatus, string? EnrolmentStatus);
public record ResultRequest(int EnrolmentId, int? FinishPosition, int? FinishTimeSeconds, string ResultStatus);
public record ResultUpdate(int? FinishPosition, int? FinishTimeSeconds, string ResultStatus);
