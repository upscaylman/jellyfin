using System;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Controller.Tests.Entities;

public class VideoMediaSourceDeduplicationTests
{
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly Mock<IFileSystem> _fileSystem;

    public VideoMediaSourceDeduplicationTests()
    {
        _libraryManager = new Mock<ILibraryManager>();
        _fileSystem = new Mock<IFileSystem>();
        _fileSystem.Setup(x => x.MakeAbsolutePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string folder, string file) => file);
        BaseItem.LibraryManager = _libraryManager.Object;
        BaseItem.FileSystem = _fileSystem.Object;
        BaseItem.Logger = Mock.Of<ILogger<BaseItem>>();
    }

    [Fact]
    public void MediaSourceCount_WithDeletedLinkedAlternate_DoesNotCount()
    {
        var deadId = Guid.NewGuid();

        _libraryManager.Setup(x => x.GetItemById(deadId)).Returns((BaseItem?)null);
        _libraryManager.Setup(x => x.FindByPath(It.IsAny<string>(), null)).Returns((BaseItem?)null);

        var primary = new Video
        {
            Id = Guid.NewGuid(),
            ForcedSortName = "Primary",
            Path = "/movies/test/test.mkv",
            LinkedAlternateVersions =
            [
                new LinkedChild { Path = "/movies/test/deleted.mkv", ItemId = deadId }
            ],
            LocalAlternateVersions = Array.Empty<string>()
        };

        // Should be 1 (just self) since the linked version is dead
        Assert.Equal(1, primary.MediaSourceCount);
    }

    [Fact]
    public void MediaSourceCount_WithValidLinkedAlternate_CountsCorrectly()
    {
        var alt = new Video { Id = Guid.NewGuid(), ForcedSortName = "Alt", Path = "/movies/test/test_4k.mkv" };

        _libraryManager.Setup(x => x.GetItemById(alt.Id)).Returns(alt);

        var primary = new Video
        {
            Id = Guid.NewGuid(),
            ForcedSortName = "Primary",
            Path = "/movies/test/test.mkv",
            LinkedAlternateVersions =
            [
                new LinkedChild { Path = alt.Path, ItemId = alt.Id }
            ],
            LocalAlternateVersions = Array.Empty<string>()
        };

        // 1 (self) + 1 (alt)
        Assert.Equal(2, primary.MediaSourceCount);
    }

    [Fact]
    public void MediaSourceCount_WithLocalAlternate_ValidatesExistence()
    {
        var localAltId = Guid.NewGuid();
        var localAlt = new Video { Id = localAltId, ForcedSortName = "Local", Path = "/movies/test/test_1080p.mkv" };
        var deadLocalId = Guid.NewGuid();

        _libraryManager.Setup(x => x.GetNewItemId("/movies/test/test_1080p.mkv", typeof(Video))).Returns(localAltId);
        _libraryManager.Setup(x => x.GetItemById(localAltId)).Returns(localAlt);
        _libraryManager.Setup(x => x.GetNewItemId("/movies/test/gone.mkv", typeof(Video))).Returns(deadLocalId);
        _libraryManager.Setup(x => x.GetItemById(deadLocalId)).Returns((BaseItem?)null);

        var primary = new Video
        {
            Id = Guid.NewGuid(),
            ForcedSortName = "Primary",
            Path = "/movies/test/test.mkv",
            LinkedAlternateVersions = Array.Empty<LinkedChild>(),
            LocalAlternateVersions = ["/movies/test/test_1080p.mkv", "/movies/test/gone.mkv"]
        };

        // 1 (self) + 1 (valid local) — dead local not counted
        Assert.Equal(2, primary.MediaSourceCount);
    }

    [Fact]
    public void GetAllItemsForMediaSources_NoDuplicates_WhenSameItemInLinkedAndLocal()
    {
        var sharedAlt = new Video
        {
            Id = Guid.NewGuid(),
            ForcedSortName = "Shared",
            Path = "/movies/test/test_4k.mkv",
            LinkedAlternateVersions = Array.Empty<LinkedChild>(),
            LocalAlternateVersions = Array.Empty<string>()
        };

        _libraryManager.Setup(x => x.GetItemById(sharedAlt.Id)).Returns(sharedAlt);
        // Local alt resolves to the SAME item via GetNewItemId
        _libraryManager.Setup(x => x.GetNewItemId("/movies/test/test_4k.mkv", typeof(Video))).Returns(sharedAlt.Id);

        var primary = new TestableVideo
        {
            Id = Guid.NewGuid(),
            ForcedSortName = "Primary",
            Path = "/movies/test/test.mkv",
            LinkedAlternateVersions =
            [
                new LinkedChild { Path = sharedAlt.Path, ItemId = sharedAlt.Id }
            ],
            // Same file referenced as local alt too
            LocalAlternateVersions = ["/movies/test/test_4k.mkv"]
        };

        var result = primary.CallGetAllItemsForMediaSources().ToList();

        // Should have exactly 2 items: primary + sharedAlt (not duplicated)
        Assert.Equal(2, result.Count);
        Assert.Equal(primary.Id, result[0].Item.Id);
        Assert.Equal(sharedAlt.Id, result[1].Item.Id);
    }

    [Fact]
    public void GetAllItemsForMediaSources_NoDuplicates_ThroughPrimaryVersionTraversal()
    {
        var alt1 = new Video
        {
            Id = Guid.NewGuid(),
            ForcedSortName = "Alt1",
            Path = "/movies/test/test_4k.mkv",
            LinkedAlternateVersions = Array.Empty<LinkedChild>(),
            LocalAlternateVersions = Array.Empty<string>()
        };

        var primaryVideo = new Video
        {
            Id = Guid.NewGuid(),
            ForcedSortName = "Primary",
            Path = "/movies/test/test.mkv",
            LinkedAlternateVersions =
            [
                new LinkedChild { Path = alt1.Path, ItemId = alt1.Id }
            ],
            LocalAlternateVersions = Array.Empty<string>()
        };

        _libraryManager.Setup(x => x.GetItemById(alt1.Id)).Returns(alt1);
        _libraryManager.Setup(x => x.GetItemById(primaryVideo.Id)).Returns(primaryVideo);

        // alt1 is an alternate that has PrimaryVersionId set
        var alternate = new TestableVideo
        {
            Id = alt1.Id,
            ForcedSortName = "Alt1",
            Path = "/movies/test/test_4k.mkv",
            PrimaryVersionId = primaryVideo.Id.ToString("N"),
            LinkedAlternateVersions = Array.Empty<LinkedChild>(),
            LocalAlternateVersions = Array.Empty<string>()
        };

        _libraryManager.Setup(x => x.GetItemById(new Guid(alternate.PrimaryVersionId))).Returns(primaryVideo);

        var result = alternate.CallGetAllItemsForMediaSources().ToList();

        // All items should appear exactly once
        var ids = result.Select(r => r.Item.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    /// Testable subclass to expose the protected GetAllItemsForMediaSources.
    /// </summary>
    private class TestableVideo : Video
    {
        public System.Collections.Generic.List<(BaseItem Item, MediaSourceType MediaSourceType)> CallGetAllItemsForMediaSources()
        {
            return GetAllItemsForMediaSources().ToList();
        }
    }
}
