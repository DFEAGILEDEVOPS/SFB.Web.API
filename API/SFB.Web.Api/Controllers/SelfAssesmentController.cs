using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Helpers.Enums;
using SFB.Web.ApplicationCore.Models;
using SFB.Web.ApplicationCore.Services.DataAccess;
using System;
using System.Threading.Tasks;

namespace SFB.Web.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SelfAssesmentController : ControllerBase
    {
        private readonly ISelfAssesmentDashboardDataService _selfAssesmentDashboardDataService;
        private readonly IFinancialDataService _financialDataService;
        private readonly IContextDataService _contextDataService;
        private readonly ILogger _logger;

        public SelfAssesmentController(
           ISelfAssesmentDashboardDataService selfAssesmentDashboardDataService, 
           IFinancialDataService financialDataService,
           IContextDataService contextDataService,
           ILogger<SelfAssesmentController> logger)
        {
            _selfAssesmentDashboardDataService = selfAssesmentDashboardDataService;
            _financialDataService = financialDataService;
            _contextDataService = contextDataService;
            _logger = logger;
        }

        [HttpGet("{urn}")]
        public async Task<ActionResult<SelfAssesmentModel>> GetAsync(int urn)
        {
            SelfAssesmentModel selfAssesmentModel = null;

            try
            {
                var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                var financeType = (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                var schoolFinancialData = await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType, CentralFinancingType.Include);
                var term = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(financeType);

                selfAssesmentModel = await BuildSelfAssesmentModel(urn, financeType, schoolContextData, schoolFinancialData, term);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            return selfAssesmentModel;
        }

        private async Task<SelfAssesmentModel> BuildSelfAssesmentModel(int urn, EstablishmentType financeType, EdubaseDataObject schoolContextData, SchoolTrustFinancialDataObject schoolFinancialData, int term)
        {
            var termYears = $"{term - 1}/{term}";
            var model = new SelfAssesmentModel();
            model.Urn = urn;
            model.Name = schoolContextData.EstablishmentName;
            model.FinancialData = schoolFinancialData;
            model.SadSizeLookup = await _selfAssesmentDashboardDataService.GetSADSizeLookupDataObject(schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.NoPupils.GetValueOrDefault(), termYears);
            model.SadFSMLookup = await _selfAssesmentDashboardDataService.GetSADFSMLookupDataObject(schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.PercentageFSM.GetValueOrDefault(), termYears);
            model.RatingForTeachingStaff = await _selfAssesmentDashboardDataService.GetSADSchoolRatingsDataObjectAsync("Teaching staff", financeType, schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.LondonWeight, model.SadSizeLookup.SizeType, model.SadFSMLookup.FSMScale, schoolFinancialData.TeachingStaff.GetValueOrDefault() / schoolFinancialData.TotalExpenditure.GetValueOrDefault(), termYears);
            return model;
        }
    }
}