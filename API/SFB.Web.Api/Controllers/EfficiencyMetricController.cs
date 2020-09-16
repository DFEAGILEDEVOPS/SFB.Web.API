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
        private readonly IContextDataService _contextDataService;

        private readonly ILogger _logger;

        public EfficiencyMetricController(IContextDataService contextDataService, 
            IEfficiencyMetricDataService efficiencyMetricDataService,
            ILogger<EfficiencyMetricController> logger)
        {
            _contextDataService = contextDataService;
            _efficiencyMetricDataService = efficiencyMetricDataService;

            _logger = logger;

        }

        // GET api/efficiencymetric/138082
        [HttpGet("{urn}")]
        public async Task<ActionResult<EfficiencyMetricParentModel>> GetAsync(int urn)
        {            
            EfficiencyMetricParentDataObject defaultSchoolEMData = null;

            try
            {
                defaultSchoolEMData = await _efficiencyMetricDataService.GetSchoolDataObjectByUrnAsync(urn);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            if (defaultSchoolEMData == null)
            {
                _logger.LogWarning("No school found with URN: {urn}", urn);
                return NoContent();
            }
            else
            {
                return new EfficiencyMetricParentModel(defaultSchoolEMData);
            }
        }
    }
}