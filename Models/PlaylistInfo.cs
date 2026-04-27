namespace Harmonify.MusicServer.Models;

public class PlaylistInfo
{
  public required string Name { get; init; }
  public required int TrackCount { get; init; }
  public required bool HasCover { get; init; }
}
