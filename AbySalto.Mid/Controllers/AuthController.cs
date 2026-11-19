using AbySalto.Mid.Application.Auth;
using AbySalto.Mid.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbySalto.Mid.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [HttpPost("register")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
        {
            var response = await authService.RegisterAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Me), null, response);
        }

        [HttpPost("login")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
            => Ok(await authService.LoginAsync(request, cancellationToken));

        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
            => Ok(await authService.GetCurrentUserAsync(cancellationToken));
    }
}
