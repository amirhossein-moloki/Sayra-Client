using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Administration.Orchestration;

namespace Sayra.Client.Shared.Fleet.Administration.Security
{
    public class ApiSecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiSecurityMiddleware> _logger;
        private readonly IAuditIntegrationService _auditService;

        // Bounded in-memory rate limiting store: Client IP -> list of request timestamps within the last minute
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> RateLimitStore = new();
        private const int MaxRequestsPerMinute = 120;

        public ApiSecurityMiddleware(RequestDelegate next, ILogger<ApiSecurityMiddleware> logger, IAuditIntegrationService auditService)
        {
            _next = next;
            _logger = logger;
            _auditService = auditService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // 1. Rate Limiting check
            if (IsRateLimited(ip))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Too Many Requests. Rate limit of 120 requests per minute exceeded.\"}");
                return;
            }

            // 2. Correlation & Tracking Headers
            if (!context.Request.Headers.TryGetValue("X-Trace-Id", out var traceId))
            {
                traceId = Guid.NewGuid().ToString("N");
                context.Request.Headers["X-Trace-Id"] = traceId;
            }
            context.Response.Headers["X-Trace-Id"] = traceId;

            if (!context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
                context.Request.Headers["X-Correlation-Id"] = correlationId;
            }
            context.Response.Headers["X-Correlation-Id"] = correlationId;

            // Extract Administrator ID if present (could be set by Jwt validation or custom header)
            if (!context.Request.Headers.TryGetValue("X-Administrator-Id", out var adminId))
            {
                adminId = "Anonymous";
            }

            // Put context items for downstream handlers to read easily
            context.Items["TraceId"] = traceId.ToString();
            context.Items["CorrelationId"] = correlationId.ToString();
            context.Items["AdministratorId"] = adminId.ToString();

            // 3. Security Headers
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

            // 4. Input Sanitization
            if (HasSanitizationViolation(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Bad Request. Suspicious input patterns detected.\"}");

                await _auditService.LogActionAsync(
                    adminId!,
                    null,
                    "SECURITY_ALERT",
                    $"Blocked malicious input pattern from IP {ip}",
                    0,
                    "SecurityError",
                    "Suspicious characters detected",
                    ip,
                    correlationId!);
                return;
            }

            // 5. Invoke downstream pipeline
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                // Simple request log
                _logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [Trace: {TraceId}, Correlation: {CorrelationId}]",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    traceId,
                    correlationId);
            }
        }

        private static bool IsRateLimited(string ip)
        {
            var now = DateTime.UtcNow;
            var queue = RateLimitStore.GetOrAdd(ip, _ => new ConcurrentQueue<DateTime>());

            // Prune old timestamps
            while (queue.TryPeek(out var timestamp) && (now - timestamp).TotalSeconds > 60)
            {
                queue.TryDequeue(out _);
            }

            if (queue.Count >= MaxRequestsPerMinute)
            {
                return true;
            }

            queue.Enqueue(now);
            return false;
        }

        private static bool HasSanitizationViolation(HttpRequest request)
        {
            // Check Query parameters for basic SQL injection or script tags
            foreach (var kvp in request.Query)
            {
                foreach (var value in kvp.Value)
                {
                    if (string.IsNullOrEmpty(value)) continue;
                    if (ContainsSuspiciousPatterns(value)) return true;
                }
            }

            // Check headers for suspicious injection patterns
            foreach (var header in request.Headers)
            {
                foreach (var value in header.Value)
                {
                    if (string.IsNullOrEmpty(value)) continue;
                    // Do not check Cookie or Auth headers which could contain complex valid values
                    if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (ContainsSuspiciousPatterns(value)) return true;
                }
            }

            return false;
        }

        private static bool ContainsSuspiciousPatterns(string value)
        {
            // Check for common malicious attack strings
            string lowerVal = value.ToLowerInvariant();
            return lowerVal.Contains("<script") ||
                   lowerVal.Contains("javascript:") ||
                   lowerVal.Contains("onload=") ||
                   lowerVal.Contains("onerror=") ||
                   lowerVal.Contains("union select") ||
                   lowerVal.Contains("or 1=1") ||
                   lowerVal.Contains("drop table") ||
                   lowerVal.Contains("alter table");
        }
    }
}
