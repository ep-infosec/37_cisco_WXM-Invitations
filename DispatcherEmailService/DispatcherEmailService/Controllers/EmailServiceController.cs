using DispatcherEmailService.Cache;
using DispatcherEmailService.Helper;
using DispatcherEmailService.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using XM.ID.Net;

namespace DispatcherEmailService.Controllers
{
    [ApiController]
    [Route("api")]
    public class EmailServiceController : ControllerBase
    {
        private readonly ConfigurationCache _configurationCache;
        private readonly BasicAuthenticationMiddleware _authentication;
        public EmailServiceController(ConfigurationCache configurationCache, BasicAuthenticationMiddleware authentication)
        {
            _configurationCache = configurationCache;
            _authentication = authentication;
        }

        [HttpPost]
        [Route("SendEmail")]
        public IActionResult SendEmail([FromHeader(Name = "Authorization")] string authToken, [FromBody] RequestBody requestBody)
        {
            try
            {
                if (!_authentication.Authenticate(authToken, requestBody))
                    return StatusCode(StatusCodes.Status401Unauthorized, "Unauthorized request");
                CustomSMTP customSMTP = new CustomSMTP(_configurationCache);
                customSMTP.SendEmail(requestBody);
                return StatusCode(StatusCodes.Status200OK, "Mail dispactched successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.ToString());
            }
        }
    }
}
