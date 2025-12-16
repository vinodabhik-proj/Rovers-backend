namespace Rovers_backend.Models
{
    public class Report
  {
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int RoversScore { get; set; }
    public string? OppoName { get; set; }
    public int OppoScore { get; set; }
    public string? Mom { get; set; }
    public string? Dod { get; set; }
    public string? Description { get; set; }
  }
}