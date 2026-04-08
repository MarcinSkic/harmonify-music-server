namespace Harmonify.MusicServer.Models;

public class TrackInfo
{
  public required int Id { get; init; }
  public required string Filename { get; init; }
  public required string Title { get; init; }
  public string? Artist { get; init; }
  public string? Album { get; init; }
  public int? DurationMs { get; init; }
  public bool HasCoverArt { get; init; }
}
