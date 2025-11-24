using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Rovers_backend.Models;

namespace Rovers_backend.Api.Middleware
{
	public class ExceptionHandler
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<ExceptionHandler> _logger;
		private readonly IHostEnvironment _env;

		public ExceptionHandler(RequestDelegate next, ILogger<ExceptionHandler> logger, IHostEnvironment env)
		{
			_next = next;
			_logger = logger;
			_env = env;
		}
		
		public async Task InvokeAsync(HttpContext context)
    {
			try
			{
				await _next(context);
			}
			catch (Exception ex)
      {
				_logger.LogError(ex, "Unhandled Exception Error Occurred.");

				context.Response.ContentType = "application/json";
				context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

				var response = _env.IsDevelopment()
					? new ErrorResponse
					{
						StatusCode = 500,
						Message = ex.Message,
						StackTrace = ex.StackTrace
					}
					: new ErrorResponse
					{
						StatusCode = 500,
						Message = "Unexpected Server Error"
					};

				var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
				var json = JsonSerializer.Serialize(response, options);

				await context.Response.WriteAsync(json);
      }
		}
  }
}