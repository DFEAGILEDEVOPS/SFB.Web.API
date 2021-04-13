using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Helpers;
using SFB.Web.ApplicationCore.Helpers.Enums;
using SFB.Web.ApplicationCore.Models;
using SFB.Web.ApplicationCore.Services.DataAccess;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SFB.Web.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SelfAssessmentController : ControllerBase
    {
        private readonly ISelfAssesmentDashboardDataService _selfAssesmentDashboardDataService;
        private readonly IFinancialDataService _financialDataService;
        private readonly IContextDataService _contextDataService;
        private readonly ILogger _logger;
        private readonly string[] _exclusionPhaseList;

        public SelfAssessmentController(
           ISelfAssesmentDashboardDataService selfAssesmentDashboardDataService, 
           IFinancialDataService financialDataService,
           IContextDataService contextDataService,
           ILogger<SelfAssessmentController> logger)
        {
            _selfAssesmentDashboardDataService = selfAssesmentDashboardDataService;
            _financialDataService = financialDataService;
            _contextDataService = contextDataService;
            _logger = logger;
            _exclusionPhaseList = new[] { "Nursery", "Pupil referral unit", "Special" };
        }

        [HttpGet("{urn}")]
        public async Task<ActionResult<SelfAssesmentModel>> GetAsync(int urn)
        {

            //try
            //{

            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
            EstablishmentType financeType;
            if (schoolContextData.IsFederation)
            {
                var selfAssesmentModel = await BuildSelfAssesmentModel(urn, schoolContextData.FederationName, EstablishmentType.Federation, null, null);
                return selfAssesmentModel;
            }
            else
            {
                financeType = (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                var selfAssesmentModel = await BuildSelfAssesmentModel(urn, schoolContextData.EstablishmentName, financeType, schoolContextData.OfstedRating, schoolContextData.OfstedLastInsp);
                return selfAssesmentModel;
            }

            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex.Message);
            //}

        }

        private async Task<SelfAssesmentModel> BuildSelfAssesmentModel(int id, string name, EstablishmentType financeType, string ofstedRating, string ofstedLastInsp)
        {
            string termYears = await GetLatestTermYears(financeType);
            var schoolFinancialData = await _financialDataService.GetSchoolFinancialDataObjectAsync(id, financeType, CentralFinancingType.Include);
            var progressScoreType = GetProgressScoreType(schoolFinancialData);
            var model = new SelfAssesmentModel(
                id,
                name,
                schoolFinancialData.OverallPhase,
                financeType.ToString(),
                schoolFinancialData.LondonWeight,
                schoolFinancialData.NoPupils.GetValueOrDefault(),
                schoolFinancialData.PercentageFSM.GetValueOrDefault(),
                ofstedRating,
                ofstedLastInsp == null ? (DateTime?)null : DateTime.ParseExact(ofstedLastInsp, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                schoolFinancialData.Progress8Measure,
                schoolFinancialData.Ks2Progress,
                progressScoreType,
                schoolFinancialData.Progress8Banding.GetValueOrDefault(),
                bool.Parse(schoolFinancialData.Has6Form),
                schoolFinancialData.TotalExpenditure.GetValueOrDefault(),
                schoolFinancialData.TotalIncome.GetValueOrDefault(),
                termYears,
                schoolFinancialData.TeachersTotal.GetValueOrDefault(),
                schoolFinancialData.TeachersLeader.GetValueOrDefault(),
                schoolFinancialData.WorkforceTotal.GetValueOrDefault(),
                schoolFinancialData.PeriodCoveredByReturn >= 12
                );

            model.SadSizeLookup = await _selfAssesmentDashboardDataService.GetSADSizeLookupDataObject(schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.NoPupils.GetValueOrDefault(), termYears);

            model.SadFSMLookup = await _selfAssesmentDashboardDataService.GetSADFSMLookupDataObject(schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.PercentageFSM.GetValueOrDefault(), termYears);

            model.AvailableScenarioTerms = await GetAllAvailableTermYears();

            model.SadAssesmentAreas = new List<SadAssesmentAreaModel>();

            await AddAssessmentArea("Spending", "Teaching staff", schoolFinancialData.TeachingStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Supply staff", schoolFinancialData.SupplyStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Education support staff", schoolFinancialData.EducationSupportStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Administrative and clerical staff", schoolFinancialData.AdministrativeClericalStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Other staff costs", schoolFinancialData.OtherStaffCosts.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Premises costs", schoolFinancialData.Premises.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Educational supplies", schoolFinancialData.EducationalSupplies.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Energy", schoolFinancialData.Energy.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);

            await AddAssessmentArea("Reserve and balance", "In-year balance", schoolFinancialData.InYearBalance.GetValueOrDefault(), schoolFinancialData.TotalIncome.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Reserve and balance", "Revenue reserve", schoolFinancialData.RevenueReserve.GetValueOrDefault(), schoolFinancialData.TotalIncome.GetValueOrDefault(), schoolFinancialData, model, termYears);

            await AddAssessmentArea("School characteristics", "Average teacher cost", null, null, schoolFinancialData, model, termYears);
            await AddAssessmentArea("School characteristics", "Senior leaders as a percentage of workforce", null, null, schoolFinancialData, model, termYears); ; ;
            await AddAssessmentArea("School characteristics", "Pupil to teacher ratio", null, null, schoolFinancialData, model, termYears);
            await AddAssessmentArea("School characteristics", "Pupil to adult ratio", null, null, schoolFinancialData, model, termYears);

            if (!_exclusionPhaseList.Contains(schoolFinancialData.OverallPhase))
            {
                await AddAssessmentArea("School characteristics", "Teacher contact ratio (less than 1)", null, null, schoolFinancialData, model, termYears);
            }

            await AddAssessmentArea("School characteristics", "Predicted percentage pupil number change in 3-5 years", null, null, schoolFinancialData, model, termYears);

            if (!_exclusionPhaseList.Contains(schoolFinancialData.OverallPhase))
            {
                await AddAssessmentArea("School characteristics", "Average Class size", null, null, schoolFinancialData, model, termYears);
            }

            return model;
        }

        private string GetProgressScoreType(SchoolTrustFinancialDataObject schoolFinancialData)
        {
            if (schoolFinancialData.Phase == "Nursery" || schoolFinancialData.Phase == "Infant and junior")
            {
                if(schoolFinancialData.Ks2Progress != null)
                {
                    return "KS2 score";
                }
                
                return null;
            }

            if (schoolFinancialData.Phase == "Special" || schoolFinancialData.Phase == "Pupil referral unit")
            {
                if (schoolFinancialData.Progress8Measure != null)
                {
                    return "Progress 8 score";
                }
                
                if (schoolFinancialData.Ks2Progress != null)
                {
                    return "KS2 score";
                }

                return null;
            }

            if(schoolFinancialData.OverallPhase == "All-through")
            {
                return "All-through";
            }

            return schoolFinancialData.OverallPhase == "Secondary" ? "Progress 8 score" : "KS2 score";
        }

        private async Task AddAssessmentArea(string areaType, string areaName, decimal? schoolData, decimal? totalForAreaType, SchoolTrustFinancialDataObject schoolFinancialData, SelfAssesmentModel model, string termYears)
        {            
            List<SADSchoolRatingsDataObject> ratings = await _selfAssesmentDashboardDataService.GetSADSchoolRatingsDataObjectAsync(areaName, schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.LondonWeight, model.SadSizeLookup?.SizeType, model.SadFSMLookup?.FSMScale, termYears);
            ratings = ratings.OrderBy(t => t.ScoreLow).ToList();
            model.SadAssesmentAreas.Add(new SadAssesmentAreaModel(areaType, areaName, schoolData, totalForAreaType, ratings));
        }

        private async Task<string> GetLatestTermYears(EstablishmentType financeType)
        {
            var term = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(financeType);
            return SchoolFormatHelpers.FinancialTermFormatAcademies(term).Replace(" ", "");            
        }

        private async Task<List<string>> GetAllAvailableTermYears()
        {
            var latestMaintainedTerm = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(EstablishmentType.Maintained);
            var availableTermYears = new List<string>();
            for (int term = latestMaintainedTerm -1; term <= latestMaintainedTerm + 3; term++)
            {
                availableTermYears.Add(SchoolFormatHelpers.FinancialTermFormatAcademies(term).Replace(" ",""));
            }

            return availableTermYears;
        }
    }
}