namespace eShop.Catalog.API.Model;

internal record CatalogFacetCount(int Id, int Count);

internal record CatalogFacets(
    IReadOnlyList<CatalogFacetCount> BrandCounts,
    IReadOnlyList<CatalogFacetCount> TypeCounts,
    int BrandTotal,
    int TypeTotal);
