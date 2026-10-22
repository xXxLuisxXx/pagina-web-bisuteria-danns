namespace Bisuteria.Web.Models;

/// <summary>Suceso con fecha persistida; no infiere fechas de estados anteriores.</summary>
public sealed record OrderTimelineEntry(DateTime AtUtc, string Label);
