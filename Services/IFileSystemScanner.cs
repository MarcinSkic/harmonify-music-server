using Harmonify.MusicServer.Models;

namespace Harmonify.MusicServer.Services;

public interface IFileSystemScanner
{
  void Scan();
  List<PlaylistInfo> GetPlaylists();
  List<TrackInfo>? GetTracks(string playlistName);
  string? GetTrackFilePath(string playlistName, int trackId);
  (byte[] Data, string MimeType)? GetCoverArt(string playlistName, int trackId);
}
