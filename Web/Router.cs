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

    api.MapGet("/tracks/{**playlist}", (string playlist, IFileSystemScanner scanner) =>
    {
      var tracks = scanner.GetTracks(Uri.UnescapeDataString(playlist));
      return tracks is null ? Results.NotFound() : Results.Ok(tracks);
    })
      .WithSummary("List tracks in a playlist")
      .WithDescription("Returns all tracks belonging to the specified playlist. Playlist name may contain slashes for nested playlists. Returns 404 if the playlist does not exist.");

    api.MapGet("/audio/{**id}", (string id, IFileSystemScanner scanner) =>
    {
      var filePath = scanner.GetTrackFilePath(Uri.UnescapeDataString(id));
      return filePath is null
        ? Results.NotFound()
        : Results.File(filePath, "audio/flac", enableRangeProcessing: true);
    })
      .WithSummary("Stream a track")
      .WithDescription("Streams the audio file for the specified track ID. Track ID is the full relative path within the music library (e.g. Beatles/Abbey Road/1). Supports HTTP range requests. Returns 404 if the track does not exist.");

    api.MapGet("/cover/{**id}", (string id, IFileSystemScanner scanner) =>
    {
      var cover = scanner.GetCoverArt(Uri.UnescapeDataString(id));
      return cover is null
        ? Results.NotFound()
        : Results.Bytes(cover.Value.Data, cover.Value.MimeType);
    })
    .AllowAnonymous()
      .WithSummary("Get track cover art")
      .WithDescription("Returns the embedded cover art image for the specified track. Returns 404 if the track or cover art does not exist.");

    app.MapGet("/api/linkPreview", async (string url, IHttpClientFactory httpClientFactory) =>
    {
      if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        return Results.BadRequest("Invalid URL");

      try
      {
        var client = httpClientFactory.CreateClient();
        var upstream = await client.GetAsync(url);
        if (!upstream.IsSuccessStatusCode)
          return Results.StatusCode((int)upstream.StatusCode);

        var contentType = upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var bytes = await upstream.Content.ReadAsByteArrayAsync();
        return Results.Bytes(bytes, contentType);
      }
      catch
      {
        return Results.StatusCode(502);
      }
    }).AllowAnonymous();

    return app;
  }
}
