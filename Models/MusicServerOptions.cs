namespace Harmonify.MusicServer.Models;

public class MusicServerOptions
{
  public const string SectionName = "MusicServer";
  public required string MusicDirectory { get; init; }
  public required string Username { get; init; }
  public required string Password { get; init; }
}
