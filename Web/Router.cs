using Harmonify.MusicServer.Services;

namespace Harmonify.MusicServer.Web;

public static class Router
{
  public static WebApplication MapAppEndpoints(this WebApplication app)
  {
    var api = app.MapGroup("/api/music").RequireAuthorization();

    api.MapGet("/", (IFileSystemScanner scanner) => Results.Ok(scanner.GetPlaylists()))
      .WithSummary("List playlists")
      .WithDescription("Returns a list of all available playlists.");

    api.MapGet("/{playlist}", (string playlist, IFileSystemScanner scanner) =>
    {
      var tracks = scanner.GetTracks(playlist);
      return tracks is null ? Results.NotFound() : Results.Ok(tracks);
    })
      .WithSummary("List tracks in a playlist")
      .WithDescription("Returns all tracks belonging to the specified playlist. Returns 404 if the playlist does not exist.");

    api.MapGet("/{playlist}/{id:int}", (string playlist, int id, IFileSystemScanner scanner) =>
    {
      var filePath = scanner.GetTrackFilePath(playlist, id);
      return filePath is null
        ? Results.NotFound()
        : Results.File(filePath, "audio/flac", enableRangeProcessing: true);
    })
      .WithSummary("Stream a track")
      .WithDescription("Streams the FLAC audio file for the specified track. Supports HTTP range requests for partial content. Returns 404 if the track does not exist.");

    api.MapGet("/{playlist}/{id:int}/cover", (string playlist, int id, IFileSystemScanner scanner) =>
    {
      var cover = scanner.GetCoverArt(playlist, id);
      return cover is null
        ? Results.NotFound()
        : Results.Bytes(cover.Value.Data, cover.Value.MimeType);
    })
    .AllowAnonymous()
      .WithSummary("Get track cover art")
      .WithDescription("Returns the embedded cover art image for the specified track. Returns 404 if the track or cover art does not exist.");

    return app;
  }
}
