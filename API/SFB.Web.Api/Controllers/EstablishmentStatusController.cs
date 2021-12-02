using Microsoft.AspNetCore.Mvc;
using SFB.Web.ApplicationCore.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SFB.Web.Api.Controllers
{
    [Route("api/")]
    [ApiController]
    public class EstablishmentStatusController : ControllerBase
    {
        private readonly IActiveEstablishmentsService _activeEstabService;

        public EstablishmentStatusController(IActiveEstablishmentsService activeEstabService)
        {
            _activeEstabService = activeEstabService;            
        }

        //[OutputCache(Duration = 28800, VaryByParam = "urn", NoStore = true)]
        [HttpGet("SchoolStatus/{urn}")]
        public async Task<IActionResult> SchoolStatus(long urn)
        {
            try
            {                
                var activeUrns = await _activeEstabService.GetAllActiveUrnsAsync();
                var found = activeUrns.Contains(urn);
                if(found)
                {
                    return Ok();
                }
                return NoContent();
            }
            catch
            {
                return StatusCode(500);
            }
        }

        [HttpGet("TrustStatus/{companyNo}")]
        public async Task<IActionResult> TrustStatus(int companyNo)
        {
            try
            {
                var activeCompanyNos = await _activeEstabService.GetAllActiveCompanyNosAsync();
                var found = activeCompanyNos.Contains(companyNo);
                if (found)
                {
                    return Ok();
                }
                return NoContent();
            }
            catch
            {
                return StatusCode(500);
            }
        }

        [HttpGet("FederationStatus/{fuid}")]
        public async Task<IActionResult> FederationStatus(long fuid)
        {
            try
            {
                var activeFuids = await _activeEstabService.GetAllActiveFuidsAsync();
                var found = activeFuids.Contains(fuid);
                if (found)
                {
                    return Ok();
                }
                return NoContent();
            }
            catch
            {
                return StatusCode(500);
            }
        }
    }
}
