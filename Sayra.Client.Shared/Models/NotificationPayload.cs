using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public enum NotificationCategory
    {
        BILLING,
        SYSTEM,
        SOCIAL,
        ADMINISTRATIVE
    }

    public enum NotificationPriority
    {
        SILENT,
        NORMAL,
        HIGH,
        CRITICAL
    }

    public enum NotificationAckStatus
    {
        Received,
        Displayed,
        Clicked,
        Dismissed,
        Failed
    }

    public class NotificationPayload
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public NotificationCategory Category { get; set; } = NotificationCategory.SYSTEM;
        public NotificationPriority Priority { get; set; } = NotificationPriority.NORMAL;
        public int TtlSeconds { get; set; } = 0; // 0 means infinite
        public string? ActionCallbackToken { get; set; }
        public string LanguageToken { get; set; } = string.Empty;
        public Dictionary<string, string> TemplateArgs { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Signature { get; set; } = string.Empty;

        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (Id == Guid.Empty)
            {
                errorMessage = "Id must not be default/empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                errorMessage = "Title is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Body))
            {
                errorMessage = "Body is required.";
                return false;
            }

            if (TtlSeconds < 0)
            {
                errorMessage = "TtlSeconds must be greater than or equal to 0.";
                return false;
            }

            if (string.IsNullOrEmpty(Signature))
            {
                errorMessage = "Signature must be a valid cryptographic string.";
                return false;
            }

            return true;
        }

        public bool IsExpired()
        {
            if (TtlSeconds <= 0) return false;
            return DateTime.UtcNow > CreatedAt.AddSeconds(TtlSeconds);
        }
    }

    public class NotificationAckPayload
    {
        public Guid NotificationId { get; set; }
        public NotificationAckStatus Status { get; set; }
        public string? ActionToken { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
    }
}
