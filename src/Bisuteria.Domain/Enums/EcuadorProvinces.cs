namespace Bisuteria.Domain.Enums;

public static class EcuadorProvinces
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Azuay", "Bolívar", "Cañar", "Carchi", "Chimborazo", "Cotopaxi", "El Oro", "Esmeraldas",
        "Galápagos", "Guayas", "Imbabura", "Loja", "Los Ríos", "Manabí", "Morona Santiago", "Napo",
        "Orellana", "Pastaza", "Pichincha", "Santa Elena", "Santo Domingo de los Tsáchilas", "Sucumbíos",
        "Tungurahua", "Zamora Chinchipe"
    ];

    public static bool Contains(string? province)
        => !string.IsNullOrWhiteSpace(province) && All.Contains(province.Trim(), StringComparer.OrdinalIgnoreCase);
}
