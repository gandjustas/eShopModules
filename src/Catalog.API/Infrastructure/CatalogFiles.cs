namespace eShop.Catalog.API.Infrastructure;

/// <summary>Файлы модуля каталога (картинки, начальные данные): рядом со сборкой, а не в корне содержимого хоста.</summary>
internal static class CatalogFiles
{
    public static string Root { get; } = Path.GetDirectoryName(typeof(CatalogFiles).Assembly.Location)!;
}
