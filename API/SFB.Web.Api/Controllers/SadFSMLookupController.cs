using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Services.DataAccess;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace SFB.Web.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SadFSMLookupController : ControllerBase
    {
        private readonly ISelfAssesmentDashboardDataService _selfAssesmentDashboardDataService;
        private readonly ILogger _logger;

        public SadFSMLookupController(
           ISelfAssesmentDashboardDataService selfAssesmentDashboardDataService,
           ILogger<SelfAssessmentController> logger)
        {
            _selfAssesmentDashboardDataService = selfAssesmentDashboardDataService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<SADFSMLookupDataObject>>> GetAsync()
        {
            try
            {
                return await _selfAssesmentDashboardDataService.GetSADFSMLookups();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return NoContent();
            }
        }
    }
}
