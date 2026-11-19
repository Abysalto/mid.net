using AbySalto.Mid.Application.Common.Dtos;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Application.Products.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AbySalto.Mid.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController(IProductService productService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<PagedResult<ProductDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts([FromQuery] ProductQuery query, CancellationToken cancellationToken)
            => Ok(await productService.GetProductsAsync(query, cancellationToken));

        [HttpGet("{id:int}")]
        [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken cancellationToken)
            => Ok(await productService.GetProductByIdAsync(id, cancellationToken));

        [HttpGet("categories")]
        [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<CategoryDto>> GetCategories()
            => Ok(productService.GetCategories());
    }
}
