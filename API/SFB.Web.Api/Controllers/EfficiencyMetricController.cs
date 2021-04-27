using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Models;
using SFB.Web.ApplicationCore.Services.DataAccess;

namespace SFB.Web.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EfficiencyMetricController : ControllerBase
    {
        private readonly IEfficiencyMetricDataService _efficiencyMetricDataService;
        private readonly ILogger _logger;

        public EfficiencyMetricController(IContextDataService contextDataService, 
            IEfficiencyMetricDataService efficiencyMetricDataService,
            ILogger<EfficiencyMetricController> logger)
        {
            _efficiencyMetricDataService = efficiencyMetricDataService;
            _logger = logger;
        }

        // GET api/efficiencymetric/138082
        [HttpGet("{urn}")]
        public async Task<ActionResult<EfficiencyMetricParentModel>> GetAsync(int urn)
        {           
            try
            {
                var defaultSchoolEMData = await _efficiencyMetricDataService.GetSchoolDataObjectByUrnAsync(urn);
                return new EfficiencyMetricParentModel(defaultSchoolEMData);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message); 
                return NoContent();
            }
        }

        // HEAD api/efficiencymetric/138082
        [HttpHead("{urn}")]
        public async Task<StatusCodeResult> HeadAsync(int urn)
        {
            try
            {
                var doesEmExist = await _efficiencyMetricDataService.GetStatusByUrn(urn);
                return doesEmExist ? (StatusCodeResult)new OkResult() : (StatusCodeResult)new NotFoundResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return NotFound();
            }
        }
    }
}