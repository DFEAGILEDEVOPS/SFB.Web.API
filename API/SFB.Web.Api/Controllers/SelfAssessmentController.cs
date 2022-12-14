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
using Microsoft.Azure.Cosmos.Linq;
using SFB.Web.Api.Models;
using StackExchange.Redis;
using Newtonsoft.Json;

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
        private readonly IConnectionMultiplexer _redis;

        public SelfAssessmentController(
           ISelfAssesmentDashboardDataService selfAssesmentDashboardDataService, 
           IFinancialDataService financialDataService,
           IContextDataService contextDataService,
           ILogger<SelfAssessmentController> logger,
           IConnectionMultiplexer redis)
        {
            _selfAssesmentDashboardDataService = selfAssesmentDashboardDataService;
            _financialDataService = financialDataService;
            _contextDataService = contextDataService;
            _logger = logger;
            _redis = redis;
            _exclusionPhaseList = new[] { "Nursery", "Pupil referral unit", "Special" };
        }
        
        
        [HttpGet("trustold/{uid}")]
        public async Task<ActionResult<TrustSelfAssessmentModel>> GetTrustAsync(int uid)
        {
            var db = _redis.GetDatabase();
            var trustFinance =
                await _financialDataService.GetTrustFinancialDataObjectByUidAsync(uid, await LatestMatTermAsync());
            var academies = (await _contextDataService.GetAcademiesByUidAsync(uid))
                .Where(x => x.OverallPhase != "16 plus")
                .OrderBy(x => x.EstablishmentName).ToList();

            var academyCount = academies.Count;

            var result = new List<SelfAssesmentModel>();

            foreach (var (establishment, i) in 
                     academies.Select((establishment, i) => (establishment, i)))
            {
                var urn = establishment.URN;
                var dbKey = $"establishmentSad-{urn}";
                var cachedResult = await db.StringGetAsync(dbKey);
                if (cachedResult.IsNull)
                {
                    var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                    var financeType =
                        (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                    var schoolFinancialData =
                        await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                            CentralFinancingType.Include);

                    var establishmentName = schoolContextData.IsFederation
                        ? schoolContextData.FederationName
                        : schoolContextData.EstablishmentName;

                    var selfAssessmentModel = await BuildSelfAssesmentModel(urn,
                        establishmentName,
                        financeType, schoolContextData, schoolFinancialData);
                    
                    result.Add(selfAssessmentModel);
                    
                    if (i < 7)
                    {
                        await db.StringSetAsync(dbKey, JsonConvert.SerializeObject(selfAssessmentModel),
                            TimeSpan.FromHours(1));
                    }
                    else // larger mat so cache results for longer
                    {
                        await db.StringSetAsync(dbKey, JsonConvert.SerializeObject(selfAssessmentModel));
                    }
                }
                else
                {
                    result.Add(JsonConvert.DeserializeObject<SelfAssesmentModel>(cachedResult));
                }
            }
                
           
            var model = new TrustSelfAssessmentModel
            {
                TrustName = trustFinance.IsFederation ? trustFinance.FederationName : trustFinance.TrustOrCompanyName,
                Uid = uid,
                CompanyNumber = trustFinance.CompanyNumber,
                Academies = result
            };
            
            return model;
        }

        [HttpGet("trust/{uid}")]
        public async Task<ActionResult<TrustSelfAssessmentModel>> GetTrustDataAsync(int uid)
        {
            var latestTerm = await LatestMatTermAsync();
            var db = _redis.GetDatabase();
            var trustAcademies = (await _contextDataService.GetAcademiesByUidAsync(uid))
                .Where(x => x.OverallPhase != "16 plus").ToList();

            var trustFinance = await _financialDataService.GetTrustFinancialDataObjectByUidAsync(uid, latestTerm);
            var academyUrns = trustAcademies.Select(x => x.URN).ToList();
            var redisKeys = academyUrns.Select(x => (RedisKey)$"establishmentSad-{x}").ToArray();
            var cachedData = await db.StringGetAsync(redisKeys);

            var model = new TrustSelfAssessmentModel
            {
                TrustName = trustFinance.IsFederation ? trustFinance.FederationName : trustFinance.TrustOrCompanyName,
                Uid = uid,
                CompanyNumber = trustFinance.CompanyNumber,
            };

            try
            {
                var result = new List<SelfAssesmentModel>();

                foreach (var redisValue in cachedData)
                {
                    if (redisValue.IsNull) continue;
                    var res = JsonConvert.DeserializeObject<SelfAssesmentModel>(redisValue);
                    academyUrns.RemoveAll(x => x == res?.Urn);
                    result.Add(res);
                }

                // build SA models for establishments not found in the cache
                if (academyUrns.Count > 0)
                {
                    var academyData = (await _contextDataService.GetMultipleSchoolDataObjectsByUrnsAsync(academyUrns))
                        .Where(x => x.OverallPhase != "16 plus").ToList();
                    var academyFinancials =
                        await _financialDataService.GetTrustSchoolsFinancialDataAsync(uid, latestTerm);

                    var sadFsmLookUps = await _selfAssesmentDashboardDataService.GetSADFSMLookups();
                    var sadSizeLookups = await _selfAssesmentDashboardDataService.GetSADSizeLookups();
                    var academyLatestTermYear = await GetLatestTermYears(EstablishmentType.Academies);
                    var academyScenarioTerms = await GetAllAvailableTermYears(EstablishmentType.Academies);
                    foreach (var academy in academyData)
                    {
                        var urn = academy.URN;
                        var dbKey = $"establishmentSad-{urn}";
                        var establishmentName = academy.IsFederation ?
                            academy.FederationName :
                            academy.EstablishmentName;
                        
                        var financeType = (EstablishmentType)Enum.Parse(typeof(EstablishmentType), academy.FinanceType);
                        var schoolFinancialData = academyFinancials.FirstOrDefault(x => x.URN == urn);

                        if (schoolFinancialData is not null)
                        {
                            var selfAssessmentModel = new SelfAssesmentModel(
                                urn,
                                establishmentName,
                                schoolFinancialData.OverallPhase,
                                financeType.ToString(),
                                schoolFinancialData.LondonWeight,
                                schoolFinancialData.NoPupils.GetValueOrDefault(),
                                schoolFinancialData.PercentageFSM.GetValueOrDefault(),
                                academy.OfstedRating,
                                FormatOfstedDate(academy.OfstedLastInsp),
                                schoolFinancialData.Progress8Measure,
                                schoolFinancialData.Ks2Progress,
                                GetProgressScoreType(schoolFinancialData),
                                schoolFinancialData.Progress8Banding.GetValueOrDefault(),
                                bool.Parse(schoolFinancialData.Has6Form),
                                schoolFinancialData.TotalExpenditure.GetValueOrDefault(),
                                schoolFinancialData.TotalIncome.GetValueOrDefault(),
                                academyLatestTermYear,
                                schoolFinancialData.TeachersTotal.GetValueOrDefault(),
                                schoolFinancialData.TeachersLeader.GetValueOrDefault(),
                                schoolFinancialData.WorkforceTotal.GetValueOrDefault(),
                                schoolFinancialData.PeriodCoveredByReturn >= 12,
                                true
                            );

                            var pupilCount = schoolFinancialData.NoPupils.GetValueOrDefault();
                            var fsmPercent = schoolFinancialData.PercentageFSM.GetValueOrDefault();
                            var has6Form = bool.Parse(schoolFinancialData.Has6Form);

                            selfAssessmentModel.SadSizeLookup = sadSizeLookups.Find(x =>
                                x.OverallPhase == schoolFinancialData.OverallPhase &&
                                x.HasSixthForm == has6Form &&
                                x.NoPupilsMin <= pupilCount && (x.NoPupilsMax == null || x.NoPupilsMax >= pupilCount) &&
                                x.Term == academyLatestTermYear);
                            
                            selfAssessmentModel.SadFSMLookup = sadFsmLookUps.Find(x =>
                                x.OverallPhase == schoolFinancialData.OverallPhase &&
                                x.HasSixthForm == has6Form &&
                                x.FSMMin <= fsmPercent && x.FSMMax >= fsmPercent &&
                                x.Term == academyLatestTermYear);
                            
                            selfAssessmentModel.AvailableScenarioTerms = academyScenarioTerms;

                            await AddAssessmentAreasToModel(academyLatestTermYear, schoolFinancialData, academy,
                                selfAssessmentModel);

                            result.Add(selfAssessmentModel);
                            
                            await db.StringSetAsync(dbKey, JsonConvert.SerializeObject(selfAssessmentModel));
                            
                        }
                        else
                        {
                            result.Add(new SelfAssesmentModel(urn, establishmentName, academy.OverallPhase,
                                financeType.ToString(),
                                academy.OfstedRating,
                                FormatOfstedDate(academy.OfstedLastInsp),
                                academy.GovernmentOfficeRegion,
                                academy.OfficialSixthForm)
                            {
                                AvailableScenarioTerms = await GetAllAvailableTermYears(financeType)
                            });
                        }
                    }
                    
                }
                model.Academies = result.OrderBy(x => x.Name).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return NoContent();
            }

            return model;
        }

        [HttpGet("{urn}")]
        public async Task<ActionResult<SelfAssesmentModel>> GetAsync(long urn)
        {
            var db = _redis.GetDatabase();
            var dbKey = $"establishmentSad-{urn}";
            var cachedResult = await db.StringGetAsync(dbKey);

            if (!cachedResult.IsNull)
            {
                return JsonConvert.DeserializeObject<SelfAssesmentModel>(cachedResult);
            }

            try
            {
                var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                var financeType =
                    (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                var schoolFinancialData =
                    await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                        CentralFinancingType.Include);

                var establishmentName = schoolContextData.IsFederation
                    ? schoolContextData.FederationName
                    : schoolContextData.EstablishmentName;

                var selfAssessmentModel = await BuildSelfAssesmentModel(urn,
                    establishmentName,
                    financeType, schoolContextData, schoolFinancialData);

                await db.StringSetAsync(dbKey, JsonConvert.SerializeObject(selfAssessmentModel),
                    TimeSpan.FromHours(1));

                return selfAssessmentModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return NoContent();
            }
        }

        private async Task<SelfAssesmentModel> BuildSelfAssesmentModel(long id, string name,
            EstablishmentType financeType, EdubaseDataObject schoolContextData,
            SchoolTrustFinancialDataObject schoolFinancialData)
        {
            string termYears = await GetLatestTermYears(financeType);

            SelfAssesmentModel model;
            if (schoolFinancialData is not null)
            {
                model = new SelfAssesmentModel(
                    id,
                    name,
                    schoolFinancialData.OverallPhase,
                    financeType.ToString(),
                    schoolFinancialData.LondonWeight,
                    schoolFinancialData.NoPupils.GetValueOrDefault(),
                    schoolFinancialData.PercentageFSM.GetValueOrDefault(),
                    schoolContextData.OfstedRating,
                    FormatOfstedDate(schoolContextData.OfstedLastInsp),
                    schoolFinancialData.Progress8Measure,
                    schoolFinancialData.Ks2Progress,
                    GetProgressScoreType(schoolFinancialData),
                    schoolFinancialData.Progress8Banding.GetValueOrDefault(),
                    bool.Parse(schoolFinancialData.Has6Form),
                    schoolFinancialData.TotalExpenditure.GetValueOrDefault(),
                    schoolFinancialData.TotalIncome.GetValueOrDefault(),
                    termYears,
                    schoolFinancialData.TeachersTotal.GetValueOrDefault(),
                    schoolFinancialData.TeachersLeader.GetValueOrDefault(),
                    schoolFinancialData.WorkforceTotal.GetValueOrDefault(),
                    schoolFinancialData.PeriodCoveredByReturn >= 12,
                    true
                );

                model.SadSizeLookup = await _selfAssesmentDashboardDataService.GetSADSizeLookupDataObject(
                    schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form),
                    schoolFinancialData.NoPupils.GetValueOrDefault(), termYears);

                model.SadFSMLookup = await _selfAssesmentDashboardDataService.GetSADFSMLookupDataObject(
                    schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form),
                    schoolFinancialData.PercentageFSM.GetValueOrDefault(), termYears);

                model.AvailableScenarioTerms = await GetAllAvailableTermYears(financeType);
            }
            else
            {
                model = new SelfAssesmentModel(id, name, schoolContextData.OverallPhase, financeType.ToString(),
                    schoolContextData.OfstedRating,
                    FormatOfstedDate(schoolContextData.OfstedLastInsp),
                    schoolContextData.GovernmentOfficeRegion,
                    schoolContextData.OfficialSixthForm);

                model.AvailableScenarioTerms = await GetAllAvailableTermYears(financeType);
            }

            await AddAssessmentAreasToModel(termYears, schoolFinancialData, schoolContextData, model);

            return model;
        }

        private static DateTime? FormatOfstedDate(string ofstedDate)
        {
            try
            {
                return DateTime.ParseExact(ofstedDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private async Task AddAssessmentAreasToModel(string termYears,
            SchoolTrustFinancialDataObject schoolFinancialData, EdubaseDataObject schoolContextData,
            SelfAssesmentModel model)
        {
            model.SadAssesmentAreas = new(); //C#9

            await AddAssessmentArea("Spending", "Teaching staff",
                schoolFinancialData?.TeachingStaff.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Supply staff", schoolFinancialData?.SupplyStaff.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Education support staff",
                schoolFinancialData?.EducationSupportStaff.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Administrative and clerical staff",
                schoolFinancialData?.AdministrativeClericalStaff.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Other staff costs",
                schoolFinancialData?.OtherStaffCosts.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Premises costs", schoolFinancialData?.Premises.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Educational supplies",
                schoolFinancialData?.EducationalSupplies.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Energy", schoolFinancialData?.Energy.GetValueOrDefault(),
                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);

            await AddAssessmentArea("Reserve and balance", "In-year balance",
                schoolFinancialData?.InYearBalance.GetValueOrDefault(),
                schoolFinancialData?.TotalIncome.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Reserve and balance", "Revenue reserve",
                schoolFinancialData?.RevenueReserve.GetValueOrDefault(),
                schoolFinancialData?.TotalIncome.GetValueOrDefault(), schoolFinancialData, model, termYears);

            await AddAssessmentArea("School characteristics", "Average teacher cost", null, null, schoolFinancialData,
                model, termYears);
            await AddAssessmentArea("School characteristics", "Senior leaders as a percentage of workforce", null, null,
                schoolFinancialData, model, termYears);
            
            
            await AddAssessmentArea("School characteristics", "Pupil to teacher ratio", null, null, schoolFinancialData,
                model, termYears);
            await AddAssessmentArea("School characteristics", "Pupil to adult ratio", null, null, schoolFinancialData,
                model, termYears);

            if (!_exclusionPhaseList.Contains(schoolFinancialData?.OverallPhase ?? schoolContextData.OverallPhase))
            {
                await AddAssessmentArea("School characteristics", "Teacher contact ratio (less than 1)", null, null,
                    schoolFinancialData, model, termYears);
            }

            await AddAssessmentArea("School characteristics", "Predicted percentage pupil number change in 3-5 years",
                null, null, schoolFinancialData, model, termYears);

            if (!_exclusionPhaseList.Contains(schoolFinancialData?.OverallPhase ?? schoolContextData.OverallPhase))
            {
                await AddAssessmentArea("School characteristics", "Average Class size", null, null, schoolFinancialData,
                    model, termYears);
            }
        }

        private string GetProgressScoreType(SchoolTrustFinancialDataObject schoolFinancialData)
        {
            if (schoolFinancialData.Phase is "Nursery" or "Infant and junior") //C#9
            {
                if (schoolFinancialData.Ks2Progress is not null) //C#9
                {
                    return "KS2 score";
                }

                return null;
            }

            if (schoolFinancialData.Phase is "Special" or "Pupil referral unit")
            {
                if (schoolFinancialData.Progress8Measure is not null)
                {
                    return "Progress 8 score";
                }

                if (schoolFinancialData.Ks2Progress is not null)
                {
                    return "KS2 score";
                }

                return null;
            }

            if (schoolFinancialData.OverallPhase is "All-through")
            {
                return "All-through";
            }

            return schoolFinancialData.OverallPhase == "Secondary" ? "Progress 8 score" : "KS2 score";
        }

        private async Task AddAssessmentArea(string areaType, string areaName, decimal? schoolData,
            decimal? totalForAreaType, SchoolTrustFinancialDataObject schoolFinancialData, SelfAssesmentModel model,
            string termYears)
        {
            List<SADSchoolRatingsDataObject> ratings =
                await _selfAssesmentDashboardDataService.GetSADSchoolRatingsDataObjectAsync(areaName,
                    schoolFinancialData?.OverallPhase, bool.Parse(schoolFinancialData?.Has6Form ?? "false"),
                    schoolFinancialData?.LondonWeight, model.SadSizeLookup?.SizeType, model.SadFSMLookup?.FSMScale,
                    termYears);
            ratings = ratings.OrderBy(t => t.ScoreLow).ToList();
            model.SadAssesmentAreas.Add(new SadAssesmentAreaModel(areaType, areaName, schoolData, totalForAreaType,
                ratings));
        }

        private async Task<string> GetLatestTermYears(EstablishmentType financeType)
        {
            var term = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(financeType);
            return SchoolFormatHelpers.FinancialTermFormatAcademies(term).Replace(" ", "");
        }

        private async Task<List<string>> GetAllAvailableTermYears(EstablishmentType establishmentType = EstablishmentType.Maintained)
        {
            var latestMaintainedTerm = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(establishmentType);

            List<string> availableTermYears = new(); //C#9
            for (int term = latestMaintainedTerm - 1; term <= latestMaintainedTerm + 3; term++)
            {
                availableTermYears.Add(SchoolFormatHelpers.FinancialTermFormatAcademies(term).Replace(" ", ""));
            }

            return availableTermYears;
        }

        private async Task<string> LatestMatTermAsync()
        {
            var latestYear = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(EstablishmentType.MAT);
            return SchoolFormatHelpers.FinancialTermFormatAcademies(latestYear);
        }
    }
}