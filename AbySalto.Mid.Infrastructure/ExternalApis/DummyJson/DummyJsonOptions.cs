using System.ComponentModel.DataAnnotations;

namespace AbySalto.Mid.Infrastructure.ExternalApis.DummyJson;

internal sealed class DummyJsonOptions
{
    public const string SectionName = "DummyJson";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://dummyjson.com/";

    [Range(0, 1440)]
    public int CacheMinutes { get; set; } = 10;

    [Range(1, 3600)]
    public int LocalCacheSeconds { get; set; } = 60;
}
