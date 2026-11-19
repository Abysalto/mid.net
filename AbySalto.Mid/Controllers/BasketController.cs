using AbySalto.Mid.Application.Baskets;
using AbySalto.Mid.Application.Baskets.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbySalto.Mid.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/basket")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class BasketController(IBasketService basketService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<BasketDto>(StatusCodes.Status200OK)]
        public async Task<ActionResult<BasketDto>> Get(CancellationToken cancellationToken)
            => Ok(await basketService.GetAsync(cancellationToken));

        [HttpPost("items")]
        [ProducesResponseType<BasketDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BasketDto>> AddItem(AddBasketItemRequest request, CancellationToken cancellationToken)
            => Ok(await basketService.AddItemAsync(request, cancellationToken));

        [HttpPut("items/{productId:int}")]
        [ProducesResponseType<BasketDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BasketDto>> UpdateItem(
            int productId, UpdateBasketItemQuantityRequest request, CancellationToken cancellationToken)
            => Ok(await basketService.UpdateItemQuantityAsync(productId, request, cancellationToken));

        [HttpDelete("items/{productId:int}")]
        [ProducesResponseType<BasketDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BasketDto>> RemoveItem(int productId, CancellationToken cancellationToken)
            => Ok(await basketService.RemoveItemAsync(productId, cancellationToken));

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Clear(CancellationToken cancellationToken)
        {
            await basketService.ClearAsync(cancellationToken);
            return NoContent();
        }
    }
}
