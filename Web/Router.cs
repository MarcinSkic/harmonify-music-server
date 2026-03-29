using Harmonify.MusicServer.Services;

namespace Harmonify.MusicServer.Web;

public static class Router
{
  public static WebApplication MapAppEndpoints(this WebApplication app)
  {
    var api = app.MapGroup("/").RequireAuthorization();

    // GET / — list playlists
    api.MapGet("/", (IFileSystemScanner scanner) => Results.Ok(scanner.GetPlaylists()));

    // GET /{playlist} — list tracks in playlist
    api.MapGet("/{playlist}", (string playlist, IFileSystemScanner scanner) =>
    {
      var tracks = scanner.GetTracks(playlist);
      return tracks is null ? Results.NotFound() : Results.Ok(tracks);
    });

    // GET /{playlist}/{id} — stream FLAC file
    api.MapGet("/{playlist}/{id}", (string playlist, string id, IFileSystemScanner scanner) =>
    {
      var filePath = scanner.GetTrackFilePath(playlist, id);
      return filePath is null
        ? Results.NotFound()
        : Results.File(filePath, "audio/flac", enableRangeProcessing: true);
    });

    // GET /{playlist}/{id}/cover — cover art
    api.MapGet("/{playlist}/{id}/cover", (string playlist, string id, IFileSystemScanner scanner) =>
    {
      var cover = scanner.GetCoverArt(playlist, id);
      return cover is null
        ? Results.NotFound()
        : Results.Bytes(cover.Value.Data, cover.Value.MimeType);
    });

    return app;
  }
}
