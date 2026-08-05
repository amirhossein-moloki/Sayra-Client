using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Sayra.Client.Shared.Fleet.Administration.Security;

namespace Sayra.Client.Shared.Fleet.Administration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdministrationController : ControllerBase
    {
        private readonly AdministrationRequestHandler _requestHandler;

        public AdministrationController(AdministrationRequestHandler requestHandler)
        {
            _requestHandler = requestHandler;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteActionDynamic()
        {
            var method = Request.Method;
            var path = Request.Path.Value + Request.QueryString.Value;

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            var token = ExtractToken(Request);
            var response = await _requestHandler.HandleRequestAsync(method, path, body, token, HttpContext.RequestAborted);

            if (response.Contains("\"error\""))
            {
                if (response.Contains("Unauthorized"))
                    return Unauthorized(response);
                return BadRequest(response);
            }

            return Content(response, "application/json");
        }

        [HttpGet("{*url}")]
        [HttpPost("{*url}")]
        [HttpPut("{*url}")]
        [HttpDelete("{*url}")]
        public async Task<IActionResult> CatchAllEndpoints(string url)
        {
            var method = Request.Method;
            var path = Request.Path.Value + Request.QueryString.Value;

            string body = string.Empty;
            if (Request.Method == "POST" || Request.Method == "PUT")
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                body = await reader.ReadToEndAsync();
            }

            var token = ExtractToken(Request);
            var response = await _requestHandler.HandleRequestAsync(method, path, body, token, HttpContext.RequestAborted);

            if (response.Contains("\"error\""))
            {
                if (response.Contains("Unauthorized"))
                    return Unauthorized(response);
                if (response.Contains("Not Found"))
                    return NotFound(response);
                return BadRequest(response);
            }

            return Content(response, "application/json");
        }

        private static string? ExtractToken(HttpRequest request)
        {
            if (request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var val = authHeader.ToString();
                if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return val.Substring(7).Trim();
                }
            }

            if (request.Query.TryGetValue("token", out var tokenQuery))
            {
                return tokenQuery.ToString();
            }

            return null;
        }
    }
}
