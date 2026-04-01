using System.Text.RegularExpressions;
using Harmonify.MusicServer.Models;
using Microsoft.Extensions.Options;

namespace Harmonify.MusicServer.Services;

public partial class FileSystemScanner(IOptions<MusicServerOptions> options, ILogger<FileSystemScanner> logger)
    : IFileSystemScanner
{
    private readonly Dictionary<string, List<TrackInfo>> _playlists = new();
    private readonly Dictionary<(string Playlist, string Id), string> _filePaths = new();
    private readonly string _musicDirectory = options.Value.MusicDirectory;

    [GeneratedRegex(@"^(\d+)\.\s+(.+)\.flac$", RegexOptions.IgnoreCase)]
    private static partial Regex FilenamePattern();

    public void Scan()
    {
        var rootDir = Path.GetFullPath(_musicDirectory);

        if (!Directory.Exists(rootDir))
        {
            logger.LogWarning("Music directory not found: {Path}", rootDir);
            return;
        }

        foreach (var dir in Directory.GetDirectories(rootDir).OrderBy(d => d))
        {
            ScanPlaylist(dir);
        }

        logger.LogInformation("Scan complete: {Count} playlists found", _playlists.Count);
    }

    private void ScanPlaylist(string directory)
    {
        var playlistName = Path.GetFileName(directory);
        var tracks = Directory.GetFiles(directory, "*.flac").OrderBy(f => f)
            .Select(file => ParseTrack(playlistName, file)).OfType<TrackInfo>().ToList();

        if (tracks.Count <= 0)
        {
            return;
        }
        
        _playlists[playlistName] = tracks;
        logger.LogInformation("Loaded playlist '{Name}' with {Count} tracks", playlistName, tracks.Count);
    }

    private TrackInfo? ParseTrack(string playlistName, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = FilenamePattern().Match(fileName);

        if (!match.Success)
        {
            logger.LogWarning("File does not match pattern '{{number}}. {{title}}.flac': {File}", fileName);
            return null;
        }

        var id = match.Groups[1].Value;
        var parsedTitle = match.Groups[2].Value;
        _filePaths[(playlistName, id)] = filePath;

        return ReadTrackMetadata(id, fileName, parsedTitle, filePath);
    }

    private TrackInfo ReadTrackMetadata(string id, string fileName, string parsedTitle, string filePath)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);

            return new TrackInfo
            {
                Id = id,
                Filename = fileName,
                Title = tagFile.Tag.Title ?? parsedTitle,
                Artist = string.Join(", ", tagFile.Tag.Performers),
                Album = tagFile.Tag.Album,
                DurationMs = (int)tagFile.Properties.Duration.TotalMilliseconds,
                HasCoverArt = tagFile.Tag.Pictures.Length > 0,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read metadata from: {File}", filePath);

            return new TrackInfo
            {
                Id = id,
                Filename = fileName,
                Title = parsedTitle,
            };
        }
    }

    public List<PlaylistInfo> GetPlaylists()
    {
        return _playlists
            .Select(p => new PlaylistInfo { Name = p.Key, TrackCount = p.Value.Count })
            .ToList();
    }

    public List<TrackInfo>? GetTracks(string playlistName)
    {
        return _playlists.GetValueOrDefault(playlistName);
    }

    public string? GetTrackFilePath(string playlistName, string trackId)
    {
        return _filePaths.GetValueOrDefault((playlistName, trackId));
    }

    public (byte[] Data, string MimeType)? GetCoverArt(string playlistName, string trackId)
    {
        var filePath = GetTrackFilePath(playlistName, trackId);
        if (filePath is null)
            return null;

        try
        {
            using var tagFile = TagLib.File.Create(filePath);

            if (tagFile.Tag.Pictures.Length == 0)
                return null;

            var picture = tagFile.Tag.Pictures[0];
            return (picture.Data.Data, picture.MimeType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read cover art from: {File}", filePath);
            return null;
        }
    }
}