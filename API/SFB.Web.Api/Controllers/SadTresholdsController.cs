using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Services.DataAccess;

namespace SFB.Web.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SadTresholdsController : ControllerBase
    {
        private readonly ISelfAssesmentDashboardDataService _selfAssesmentDashboardDataService;
        private readonly ILogger _logger;

        public SadTresholdsController(
           ISelfAssesmentDashboardDataService selfAssesmentDashboardDataService,
           ILogger<SelfAssessmentController> logger)
        {
            _selfAssesmentDashboardDataService = selfAssesmentDashboardDataService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<SADSchoolRatingsDataObject>>> GetAsync(string areaName, string overallPhase, bool has6Form, string londonWeight, string sizeType, string fsmScale, string termYears)
        {
            var ratings = await _selfAssesmentDashboardDataService.GetSADSchoolRatingsDataObjectAsync(areaName, overallPhase, has6Form, londonWeight, sizeType, fsmScale, termYears);
            ratings = ratings.OrderBy(t => t.ScoreLow).ToList();

            return ratings;
        }
    }
}
