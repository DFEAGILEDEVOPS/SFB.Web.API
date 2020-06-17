using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Services.DataAccess;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SFB.Web.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SadSizeLookupController : ControllerBase
    {
        private readonly ISelfAssesmentDashboardDataService _selfAssesmentDashboardDataService;
        private readonly ILogger _logger;

        public SadSizeLookupController(
           ISelfAssesmentDashboardDataService selfAssesmentDashboardDataService,
           ILogger<SelfAssessmentController> logger)
        {
            _selfAssesmentDashboardDataService = selfAssesmentDashboardDataService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<SADSizeLookupDataObject>>> GetAsync()
        {
            //try
            //{
            return await _selfAssesmentDashboardDataService.GetSADSizeLookups();
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex.Message);
            //}
        }
    }
}
