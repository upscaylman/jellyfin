using System.Text.Json;
using MediaBrowser.Controller.Entities;
using Xunit;

namespace Jellyfin.Controller.Tests.Entities;

public class JsonLinkedChildArrayConverterTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Converters = { new JsonLinkedChildArrayConverter() }
    };

    [Fact]
    public void Read_LegacyStringArray_ReturnsLinkedChildren()
    {
        var json = """["/movies/film.mkv", "/movies/film_4k.mkv"]""";

        var result = JsonSerializer.Deserialize<LinkedChild[]>(json, _options);

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("/movies/film.mkv", result[0].Path);
        Assert.Equal("/movies/film_4k.mkv", result[1].Path);
    }

    [Fact]
    public void Read_NewObjectArray_ReturnsLinkedChildren()
    {
        var json = """[{"Path":"/movies/film.mkv","Type":0},{"Path":"/movies/film_4k.mkv","Type":0}]""";

        var result = JsonSerializer.Deserialize<LinkedChild[]>(json, _options);

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("/movies/film.mkv", result[0].Path);
        Assert.Equal("/movies/film_4k.mkv", result[1].Path);
    }

    [Fact]
    public void Read_EmptyArray_ReturnsEmpty()
    {
        var json = "[]";

        var result = JsonSerializer.Deserialize<LinkedChild[]>(json, _options);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Read_NullToken_ReturnsEmpty()
    {
        var json = "null";

        var result = JsonSerializer.Deserialize<LinkedChild[]>(json, _options);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Read_MixedArray_ReturnsAll()
    {
        var json = """["/movies/legacy.mkv", {"Path":"/movies/new.mkv","Type":0}]""";

        var result = JsonSerializer.Deserialize<LinkedChild[]>(json, _options);

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("/movies/legacy.mkv", result[0].Path);
        Assert.Equal("/movies/new.mkv", result[1].Path);
    }

    [Fact]
    public void Write_RoundTrips_AsObjectArray()
    {
        var children = new[]
        {
            new LinkedChild { Path = "/movies/film.mkv" },
            new LinkedChild { Path = "/movies/film_4k.mkv" }
        };

        var json = JsonSerializer.Serialize(children, _options);
        var result = JsonSerializer.Deserialize<LinkedChild[]>(json, _options);

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("/movies/film.mkv", result[0].Path);
        Assert.Equal("/movies/film_4k.mkv", result[1].Path);
    }
}
