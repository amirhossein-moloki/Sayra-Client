using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Fleet.Administration.Security;
using Sayra.Client.Shared.Fleet.Administration.Orchestration;
using Sayra.Client.Shared.Fleet.Administration.Queries;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Administration
{
    public delegate Task<string> ApiRouteHandler(Dictionary<string, string> routeParams, Dictionary<string, string> queryParams, string requestBody, AdminUser user, CancellationToken ct);

    public class AdministrationEndpointRegistry
    {
        private readonly Dictionary<(string Method, string Pattern), ApiRouteHandler> _routes = new();

        public void MapRoute(string method, string pattern, ApiRouteHandler handler)
        {
            _routes[(method.ToUpperInvariant(), pattern.ToLowerInvariant())] = handler;
        }

        public (ApiRouteHandler? Handler, Dictionary<string, string> RouteParams) Resolve(string method, string path)
        {
            method = method.ToUpperInvariant();
            path = path.ToLowerInvariant().Trim('/');

            foreach (var route in _routes)
            {
                var routePattern = route.Key.Pattern.Trim('/');
                if (route.Key.Method != method) continue;

                var routeParts = routePattern.Split('/');
                var pathParts = path.Split('/');

                if (routeParts.Length != pathParts.Length) continue;

                var routeParams = new Dictionary<string, string>();
                bool match = true;

                for (int i = 0; i < routeParts.Length; i++)
                {
                    if (routeParts[i].StartsWith('{') && routeParts[i].EndsWith('}'))
                    {
                        var paramName = routeParts[i].Substring(1, routeParts[i].Length - 2);
                        routeParams[paramName] = Uri.UnescapeDataString(pathParts[i]);
                    }
                    else if (routeParts[i] != pathParts[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return (route.Value, routeParams);
                }
            }

            return (null, new Dictionary<string, string>());
        }
    }

    public class AdministrationRequestHandler
    {
        private readonly AdministrationEndpointRegistry _registry;
        private readonly IAuthenticationService _authService;
        private readonly IAuthorizationService _authzService;

        public AdministrationRequestHandler(
            AdministrationEndpointRegistry registry,
            IAuthenticationService authService,
            IAuthorizationService authzService)
        {
            _registry = registry;
            _authService = authService;
            _authzService = authzService;
        }

        public async Task<string> HandleRequestAsync(
            string method,
            string fullPathWithQuery,
            string requestPayloadJson,
            string? token,
            CancellationToken ct = default)
        {
            // Parse path and query
            var queryIndex = fullPathWithQuery.IndexOf('?');
            var path = queryIndex >= 0 ? fullPathWithQuery.Substring(0, queryIndex) : fullPathWithQuery;
            var queryString = queryIndex >= 0 ? fullPathWithQuery.Substring(queryIndex + 1) : string.Empty;

            var queryParams = ParseQueryString(queryString);

            // Hardened Input Sanitization check at Gateway Dispatch level
            if (ContainsSuspiciousPatterns(fullPathWithQuery) || ContainsSuspiciousPatterns(requestPayloadJson))
            {
                return "Suspicious input patterns detected";
            }

            // Resolve endpoint
            var (handler, routeParams) = _registry.Resolve(method, path);
            if (handler == null)
            {
                return JsonSerializer.Serialize(new { error = $"Not Found. Route {method} {path} is not registered." });
            }

            // Authenticate
            if (string.IsNullOrWhiteSpace(token))
            {
                return JsonSerializer.Serialize(new { error = "Unauthorized. Token is missing." });
            }

            var adminUser = await _authService.ValidateTokenAsync(token);
            if (adminUser == null)
            {
                return JsonSerializer.Serialize(new { error = "Unauthorized. Invalid or expired token." });
            }

            // Execute
            try
            {
                return await handler(routeParams, queryParams, requestPayloadJson, adminUser, ct);
            }
            catch (FluentValidation.ValidationException vex)
            {
                return JsonSerializer.Serialize(new { error = "Validation Error", details = vex.Errors.Select(e => e.ErrorMessage).ToList() });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = "Internal Server Error", message = ex.Message });
            }
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query)) return dict;

            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=');
                if (kv.Length == 2)
                {
                    dict[kv[0]] = Uri.UnescapeDataString(kv[1]);
                }
                else if (kv.Length == 1)
                {
                    dict[kv[0]] = string.Empty;
                }
            }
            return dict;
        }

        private static bool ContainsSuspiciousPatterns(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
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

    public class AdministrationApiService : IAdministrationApiService
    {
        private readonly AdministrationRequestHandler _handler;
        private readonly IAuthenticationService _authService;

        public AdministrationApiService(
            AdministrationRequestHandler handler,
            IAuthenticationService authService)
        {
            _handler = handler;
            _authService = authService;
        }

        public async Task<string> HandleApiRequestAsync(string apiPath, string requestPayloadJson, CancellationToken ct = default)
        {
            // Supports testing API simulation internally
            // Format: "METHOD PATH" as apiPath (e.g., "GET /api/fleet/machines?page=1") or extract token if passed
            var method = "GET";
            var path = apiPath;
            string? token = null;

            var spaceIndex = apiPath.IndexOf(' ');
            if (spaceIndex > 0)
            {
                method = apiPath.Substring(0, spaceIndex);
                path = apiPath.Substring(spaceIndex + 1);
            }

            // For internal loopback/IPC testing, we can simulate an authenticated session
            var tokenHeaderIndex = path.IndexOf("token=");
            if (tokenHeaderIndex > 0)
            {
                token = path.Substring(tokenHeaderIndex + 6);
                path = path.Substring(0, tokenHeaderIndex - 1);
            }
            else
            {
                // Authenticate with default admin-01 user to bypass manual header pass in local service calls
                var defaultAdmin = new AdminUser { AdministratorId = "admin-01", Username = "admin", Role = AdminRole.SuperAdministrator };
                token = await _authService.GenerateTokenAsync(defaultAdmin);
            }

            return await _handler.HandleRequestAsync(method, path, requestPayloadJson, token, ct);
        }
    }
}
