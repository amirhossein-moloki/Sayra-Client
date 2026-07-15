namespace Sayra.Client.LocalAdmin.Models
{
    public class AdminAuthenticationResult
    {
        public bool Success { get; set; }
        public string? ErrorReason { get; set; }
        public string? SessionToken { get; set; }
    }
}
