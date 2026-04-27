using System.Text.RegularExpressions;
using Harmonify.MusicServer.Models;
using Microsoft.Extensions.Options;

namespace Harmonify.MusicServer.Services;

public partial class FileSystemScanner(IOptions<MusicServerOptions> options, ILogger<FileSystemScanner> logger)
    : IFileSystemScanner
{
    private readonly Dictionary<string, List<TrackInfo>> _playlists = new();
    private readonly Dictionary<string, string> _filePaths = new();
    private readonly Dictionary<string, string?> _playlistCoverPaths = new();
    private readonly string _musicDirectory = options.Value.MusicDirectory;
    private readonly int _maxPlaylistDepth = options.Value.MaxPlaylistDepth;

    private static readonly string[] SupportedFileTypes = ["*.flac", "*.mp3"];

    private static readonly string SupportedExtensions =
        string.Join("|", SupportedFileTypes.Select(supportedType => supportedType[2..]));

    [GeneratedRegex(@"^(\d+)\.(\d+)\.?\s*(.+)\.(flac|mp3)$", RegexOptions.IgnoreCase)]
    private static partial Regex MultiDiscPattern();

    [GeneratedRegex(@"^(\d+)\.\s*(.+)\.(flac|mp3)$", RegexOptions.IgnoreCase)]
    private static partial Regex StandardPattern();

    public void Scan()
    {
        var rootDir = Path.GetFullPath(_musicDirectory);

        if (!Directory.Exists(rootDir))
        {
            logger.LogWarning("Music directory not found: {Path}", rootDir);
            return;
        }

        ScanDirectory(rootDir, "", 0);

        logger.LogInformation("Scan complete: {Count} playlists found", _playlists.Count);
    }

    private void ScanDirectory(string absoluteDir, string relativeDir, int depth)
    {
        var subDirs = Directory.GetDirectories(absoluteDir).OrderBy(d => d).ToList();

        var directTracks = new List<TrackInfo>();
        if (relativeDir != "")
        {
            directTracks = SupportedFileTypes
                .SelectMany(ext => Directory.GetFiles(absoluteDir, ext))
                .OrderBy(f => f)
                .Select(file => ParseTrack(relativeDir, file))
                .OfType<TrackInfo>()
                .ToList();
        }

        var subTracks = new List<TrackInfo>();
        if (depth < _maxPlaylistDepth)
        {
            foreach (var subDir in subDirs)
            {
                var subDirName = Path.GetFileName(subDir);
                var subRelative = relativeDir == "" ? subDirName : $"{relativeDir}/{subDirName}";
                ScanDirectory(subDir, subRelative, depth + 1);

                if (_playlists.TryGetValue(subRelative, out var subPlaylistTracks))
                    subTracks.AddRange(subPlaylistTracks);
            }
        }

        if (relativeDir != "")
        {
            var allTracks = directTracks.Concat(subTracks).ToList();
            if (allTracks.Count > 0)
            {
                _playlists[relativeDir] = allTracks;
                logger.LogInformation("Loaded playlist '{Name}' with {Count} tracks", relativeDir, allTracks.Count);

                var coverFile = Directory.EnumerateFiles(absoluteDir)
                    .FirstOrDefault(f =>
                        Path.GetFileName(f).Equals("playlist.jpg", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(f).Equals("playlist.png", StringComparison.OrdinalIgnoreCase));

                if (coverFile is not null)
                    _playlistCoverPaths[relativeDir] = coverFile;
                else if (allTracks[0].HasCoverArt)
                    _playlistCoverPaths[relativeDir] = null;
            }
        }
    }

    private TrackInfo? ParseTrack(string relativeDir, string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        var multiDisc = MultiDiscPattern().Match(fileName);
        if (multiDisc.Success)
        {
            var disc = int.Parse(multiDisc.Groups[1].Value);
            var track = int.Parse(multiDisc.Groups[2].Value);
            var parsedTitle = multiDisc.Groups[3].Value;
            var id = $"{relativeDir}/{disc * 1000 + track}";
            _filePaths[id] = filePath;
            return ReadTrackMetadata(id, fileName, parsedTitle, filePath);
        }

        var standard = StandardPattern().Match(fileName);
        if (standard.Success)
        {
            var num = int.Parse(standard.Groups[1].Value);
            var parsedTitle = standard.Groups[2].Value;
            var id = $"{relativeDir}/{num}";
            _filePaths[id] = filePath;
            return ReadTrackMetadata(id, fileName, parsedTitle, filePath);
        }

        var ext = Path.GetExtension(fileName);
        var fallbackName = Path.GetFileNameWithoutExtension(fileName);
        var fallbackId = $"{relativeDir}/{fallbackName}";
        logger.LogWarning("File does not match known patterns, using filename as ID: {File}", fileName);
        _filePaths[fallbackId] = filePath;
        return ReadTrackMetadata(fallbackId, fileName, fallbackName, filePath);
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
            .Select(p => new PlaylistInfo
            {
                Name = p.Key,
                TrackCount = p.Value.Count,
                HasCover = _playlistCoverPaths.ContainsKey(p.Key),
            })
            .ToList();
    }

    public List<TrackInfo>? GetTracks(string playlistName)
    {
        return _playlists.GetValueOrDefault(playlistName);
    }

    public string? GetTrackFilePath(string trackId)
    {
        return _filePaths.GetValueOrDefault(trackId);
    }

    public (byte[] Data, string MimeType)? GetCoverArt(string trackId)
    {
        var filePath = GetTrackFilePath(trackId);
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

    public (byte[] Data, string MimeType)? GetPlaylistCover(string playlistName)
    {
        if (!_playlists.ContainsKey(playlistName))
            return null;

        if (!_playlistCoverPaths.TryGetValue(playlistName, out var path))
            return null;

        if (path is not null)
        {
            var mime = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            return (File.ReadAllBytes(path), mime);
        }

        var firstTrackId = _playlists[playlistName][0].Id;
        return GetCoverArt(firstTrackId);
    }
}
