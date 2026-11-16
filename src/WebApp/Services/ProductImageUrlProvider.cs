using eShop.WebAppComponents.Services;

namespace eShop.WebApp.Services;

// The picture is served by the catalog API in this very process: the /product-images proxy is not needed
public class ProductImageUrlProvider : IProductImageUrlProvider
{
    public string GetProductImageUrl(int productId)
        => $"api/catalog/items/{productId}/pic?api-version=2.0";
}
