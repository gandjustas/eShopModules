using Microsoft.AspNetCore.Http.HttpResults;
using Contract = eShop.WebAppComponents.Catalog;

namespace eShop.Catalog.API.Services;

// The catalog for the storefront in the same process: the same methods that answer HTTP, without the network and JSON.
// A scope per call, like an HTTP request: in Blazor Server the storefront scope lives as long as the circuit,
// and one DbContext cannot serve parallel calls of a page
internal sealed class LocalCatalogService(IServiceScopeFactory scopes) : WebAppComponents.Services.ICatalogService
{
    public Task<Contract.CatalogItem?> GetCatalogItem(int id) => InScope(async services =>
        (await CatalogApi.GetItemById(null!, services, id)).Result is Ok<CatalogItem> { Value: { } item }
            ? Map(item)
            : null);

    public Task<Contract.CatalogResult> GetCatalogItems(int pageIndex, int pageSize, int[]? brands, int[]? types) =>
        InScope(async services => Map((await CatalogApi.GetAllItems(new PaginationRequest(pageSize, pageIndex), services, null, types, brands)).Value!));

    public Task<List<Contract.CatalogItem>> GetCatalogItems(IEnumerable<int> ids) =>
        InScope(async services => (await CatalogApi.GetItemsByIds(services, [.. ids])).Value!.Select(Map).ToList());

    public Task<Contract.CatalogResult> GetCatalogItemsWithSemanticRelevance(int page, int take, string text) =>
        InScope(async services => (await CatalogApi.GetItemsBySemanticRelevance(new PaginationRequest(take, page), services, text)).Result switch
        {
            Ok<PaginatedItems<CatalogItem>> { Value: { } items } => Map(items),
            // Over HTTP the client followed the redirect to the search by name
            _ => Map((await CatalogApi.GetItemsByName(new PaginationRequest(take, page), services, text)).Value!),
        });

    public Task<IEnumerable<Contract.CatalogBrand>> GetBrands() => InScope(async services =>
        (await services.Context.CatalogBrands.OrderBy(x => x.Brand).ToListAsync())
            .Select(brand => new Contract.CatalogBrand(brand.Id, brand.Brand)));

    public Task<IEnumerable<Contract.CatalogItemType>> GetTypes() => InScope(async services =>
        (await services.Context.CatalogTypes.OrderBy(x => x.Type).ToListAsync())
            .Select(type => new Contract.CatalogItemType(type.Id, type.Type)));

    public Task<Contract.CatalogFacets> GetCatalogFacets(int[]? brands, int[]? types) => InScope(async services =>
    {
        var facets = (await CatalogApi.GetCatalogFacets(services, types, brands)).Value!;
        return new Contract.CatalogFacets(
            [.. facets.BrandCounts.Select(c => new Contract.CatalogFacetCount(c.Id, c.Count))],
            [.. facets.TypeCounts.Select(c => new Contract.CatalogFacetCount(c.Id, c.Count))],
            facets.BrandTotal,
            facets.TypeTotal);
    });

    private async Task<T> InScope<T>(Func<CatalogServices, Task<T>> call)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await call(ActivatorUtilities.CreateInstance<CatalogServices>(scope.ServiceProvider));
    }

    private static Contract.CatalogResult Map(PaginatedItems<CatalogItem> page) =>
        new(page.PageIndex, page.PageSize, (int)page.Count, [.. page.Data.Select(Map)]);

    private static Contract.CatalogItem Map(CatalogItem item) => new(
        item.Id,
        item.Name,
        item.Description!,
        item.Price,
        null!,
        item.CatalogBrandId,
        item.CatalogBrand is { } brand ? new(brand.Id, brand.Brand) : null!,
        item.CatalogTypeId,
        item.CatalogType is { } type ? new(type.Id, type.Type) : null!);
}
