using AbySalto.Mid.Application.Common.Dtos;
using AbySalto.Mid.Application.Products.Dtos;

namespace AbySalto.Mid.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductDto> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
    IReadOnlyList<CategoryDto> GetCategories();
}
