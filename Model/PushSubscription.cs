using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace mps.Model;

/// <summary>
/// Stores push notification subscription data for a user
/// </summary>
[Index(nameof(Endpoint), IsUnique = true)]
public class PushSubscription : EntityBase
{
    /// <summary>
    /// The user ID this subscription belongs to
    /// </summary>
    [Required]
    public string UserId { get; set; }

    /// <summary>
    /// The push service endpoint URL
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Endpoint { get; set; }

    /// <summary>
    /// The client public key (P256DH)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string P256dh { get; set; }

    /// <summary>
    /// The authentication secret
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Auth { get; set; }

    /// <summary>
    /// When this subscription was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional: User agent string to identify the device/browser
    /// </summary>
    [MaxLength(500)]
    public string UserAgent { get; set; }

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public virtual ApplicationUser User { get; set; }
}
