namespace Rovers_backend.Models
{
	public class ErrorResponse
	{
    public int StatusCode { get; set; }
		public required string Message { get; set; }
		public string? StackTrace { get; set; }
  }
}