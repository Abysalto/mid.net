using AbySalto.Mid.Application.Favorites;
using AbySalto.Mid.Application.Favorites.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbySalto.Mid.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/favorites")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class FavoritesController(IFavoriteService favoriteService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<FavoriteDto>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<FavoriteDto>>> Get(CancellationToken cancellationToken)
            => Ok(await favoriteService.GetAsync(cancellationToken));

        [HttpPut("{productId:int}")]
        [ProducesResponseType<FavoriteDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<FavoriteDto>> Add(int productId, CancellationToken cancellationToken)
            => Ok(await favoriteService.AddAsync(productId, cancellationToken));

        [HttpDelete("{productId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Remove(int productId, CancellationToken cancellationToken)
        {
            await favoriteService.RemoveAsync(productId, cancellationToken);
            return NoContent();
        }
    }
}
