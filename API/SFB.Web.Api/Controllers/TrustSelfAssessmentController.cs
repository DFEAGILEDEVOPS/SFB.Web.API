using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.Api.Models;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Helpers;
using SFB.Web.ApplicationCore.Helpers.Enums;
using SFB.Web.ApplicationCore.Models;
using SFB.Web.ApplicationCore.Services.DataAccess;
using SFB.Web.Api.Helpers.Enums;

namespace SFB.Web.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrustSelfAssessmentController : ControllerBase
    {
        private readonly ISelfAssesmentDashboardDataService _selfAssessmentDashboardDataService;
        private readonly IFinancialDataService _financialDataService;
        private readonly IContextDataService _contextDataService;
        private readonly ILogger _logger;
        private readonly string[] _exclusionPhaseList;

        public TrustSelfAssessmentController(
            ISelfAssesmentDashboardDataService selfAssessmentDashboardDataService,
            IFinancialDataService financialDataService,
            IContextDataService contextDataService,
            ILogger<TrustSelfAssessmentController> logger)
        {
            _selfAssessmentDashboardDataService = selfAssessmentDashboardDataService;
            _financialDataService = financialDataService;
            _contextDataService = contextDataService;
            _logger = logger;
            _exclusionPhaseList = new[] { "Nursery", "Pupil referral unit", "Special" };
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

        private string ProgressDescription(decimal? progressScore, BicProgressScoreType progressScoreType,
            decimal? p8Banding)
        {
            if (progressScoreType == BicProgressScoreType.P8)
            {
                if (p8Banding == 5)
                {
                    return "well below average";
                }

                if (p8Banding == 4)
                {
                    return "below average";
                }

                if (p8Banding == 3)
                {
                    return "average";
                }

                if (p8Banding == 2)
                {
                    return "above average";
                }

                if (p8Banding == 1)
                {
                    return "well above average";
                }

                if (p8Banding == 0)
                {
                    return "unknown";
                }
            }
            else if (progressScoreType == BicProgressScoreType.KS2)
            {
                if (progressScore < -3m)
                {
                    return "well below average";
                }
                else if (progressScore >= -3m && progressScore < -2m)
                {
                    return "below average";
                }
                else if (progressScore >= -2m && progressScore <= 2m)
                {
                    return "average";
                }
                else if (progressScore > 2m && progressScore <= 3m)
                {
                    return "above average";
                }
                else if (progressScore > 3m)
                {
                    return "well above average";
                }
            }
            else
            {
                return "n/a";
            }
            return "n/a";
        }

        private async Task<string> LatestMatTermAsync()
        {
            var latestYear = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(EstablishmentType.MAT);
            return SchoolFormatHelpers.FinancialTermFormatAcademies(latestYear);
        }

        private async Task<string> GetLatestTermYears(EstablishmentType financeType)
        {
            var term = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(financeType);
            return SchoolFormatHelpers.FinancialTermFormatAcademies(term).Replace(" ", "");
        }
        
        private async Task<List<SADSchoolRatingsDataObject>> GetEstablishmentRatingsAsync(
            string areaName,
            SchoolTrustFinancialDataObject schoolFinancialData,
            TrustSelfAssessmentModel model,
            string termYears)
        {
            List<SADSchoolRatingsDataObject> ratings =
                await _selfAssessmentDashboardDataService.GetSADSchoolRatingsDataObjectAsync(areaName,
                    schoolFinancialData?.OverallPhase, bool.Parse(schoolFinancialData?.Has6Form ?? "false"),
                    schoolFinancialData?.LondonWeight, model.SadSizeLookup?.SizeType, model.SadFSMLookup?.FSMScale,
                    termYears);
            ratings = ratings.OrderBy(t => t.ScoreLow).ToList();

            return ratings;
        }

        [HttpGet("{uid}/{category}")]
        public async Task<ActionResult<TrustSelfAssessmentModel>> GetTrustAsync(int uid, Enums.SadCategories category)
        {
            try
            {


                var trustFinance =
                    await _financialDataService.GetTrustFinancialDataObjectByUidAsync(uid, await LatestMatTermAsync());
                var academies = (await _contextDataService.GetAcademiesByUidAsync(uid)).ToList();
                var urns = academies.Select(a => a.URN).ToList();

                Task<EdubaseDataObject>[] tasks =
                    urns.Select(id => _contextDataService.GetSchoolDataObjectByUrnAsync(id)).ToArray();
                EdubaseDataObject[] trustEstablishments = await Task.WhenAll(tasks);


                TrustSelfAssessmentModel model = new TrustSelfAssessmentModel
                {
                    TrustName = trustFinance.TrustOrCompanyName,
                    Uid = uid,
                    CompanyNumber = trustFinance.CompanyNumber,
                    Academies = trustEstablishments.ToList(),
                };

                switch (category)
                {
                    case Enums.SadCategories.InYearBalance:
                        model.InYearBalance = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "In-year balance",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Reserve and balance",
                                "In-year balance",
                                schoolFinancialData?.InYearBalance.GetValueOrDefault(),
                                schoolFinancialData?.TotalIncome.GetValueOrDefault(),
                                ratings);

                            model.InYearBalance.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.RevenueReserve:
                        model.RevenueReserve = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Revenue reserve",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Reserve and balance",
                                "Revenue reserve",
                                schoolFinancialData?.RevenueReserve.GetValueOrDefault(),
                                schoolFinancialData?.TotalIncome.GetValueOrDefault(),
                                ratings);

                            model.RevenueReserve.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.TeachingStaff:
                        model.TeachingStaff = new List<KeyValuePair<int, SadAssesmentAreaModel>>();
                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Teaching staff",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Teaching staff",
                                schoolFinancialData?.TeachingStaff.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.TeachingStaff.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.SupplyStaff:
                        model.SupplyStaff = new List<KeyValuePair<int, SadAssesmentAreaModel>>();
                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Supply staff",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Supply staff",
                                schoolFinancialData?.SupplyStaff.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.SupplyStaff.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.EducationSupportStaff:
                        model.EducationSupportStaff = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Education support staff",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Education support staff",
                                schoolFinancialData?.EducationSupportStaff.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.SupplyStaff.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.AdminAndClericalStaff:
                        model.AdminAndClericalStaff = new List<KeyValuePair<int, SadAssesmentAreaModel>>();
                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Administrative and clerical staff",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Administrative and clerical staff",
                                schoolFinancialData?.AdministrativeClericalStaff.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.AdminAndClericalStaff.Add(
                                new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.OtherStaffCosts:
                        model.OtherStaffCosts = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Other staff costs",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Other staff costs",
                                schoolFinancialData?.OtherStaffCosts.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.OtherStaffCosts.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.PremisesCosts:
                        model.PremisesCosts = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Premises costs",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Premises costs",
                                schoolFinancialData?.Premises.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.PremisesCosts.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.EducationalSupplies:

                        model.EducationalSupplies = new List<KeyValuePair<int, SadAssesmentAreaModel>>();
                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Educational supplies",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Educational supplies",
                                schoolFinancialData?.EducationalSupplies.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.EducationalSupplies.Add(
                                new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.Energy:
                        model.Energy = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Energy",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "Spending",
                                "Energy",
                                schoolFinancialData?.EducationalSupplies.GetValueOrDefault(),
                                schoolFinancialData?.TotalExpenditure.GetValueOrDefault(),
                                ratings);

                            model.Energy.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.AverageTeacherCost:
                        model.AverageTeacherCost = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Average teacher cost",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "School characteristics",
                                "Average teacher cost",
                                null,
                                null,
                                ratings);

                            model.AverageTeacherCost.Add(
                                new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.SeniorLeaders:
                        model.SeniorLeaders = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Senior leaders as a percentage of workforce",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "School characteristics",
                                "Senior leaders as a percentage of workforce",
                                null,
                                null,
                                ratings);

                            model.SeniorLeaders.Add(new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.PupilToTeacherRatio:
                        model.PupilToTeacherRatio = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Pupil to teacher ratio",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "School characteristics",
                                "Pupil to teacher ratio",
                                null,
                                null,
                                ratings);

                            model.PupilToTeacherRatio.Add(
                                new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.PupilToAdultRatio:
                        model.PupilToAdultRatio = new List<KeyValuePair<int, SadAssesmentAreaModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);
                            string termYears = await GetLatestTermYears(financeType);

                            var ratings =
                                await GetEstablishmentRatingsAsync(
                                    "Pupil to adult ratio",
                                    schoolFinancialData,
                                    model,
                                    termYears);

                            var sadModel = new SadAssesmentAreaModel(
                                "School characteristics",
                                "Pupil to adult ratio",
                                null,
                                null,
                                ratings);

                            model.PupilToAdultRatio.Add(
                                new KeyValuePair<int, SadAssesmentAreaModel>((int)urn, sadModel));
                        }

                        break;

                    case Enums.SadCategories.Ks2Score:
                    case Enums.SadCategories.Progress8Score:

                        model.ProgressScore = new List<KeyValuePair<int, ProgressScoreModel>>();

                        foreach (var urn in urns)
                        {
                            var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);
                            var financeType =
                                (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);
                            var schoolFinancialData =
                                await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType,
                                    CentralFinancingType.Include);

                            var progressModel = new ProgressScoreModel
                            {
                                Ks2Score = schoolFinancialData.Ks2Progress,
                                Ks2ScoreDescription = ProgressDescription(schoolFinancialData.Ks2Progress,
                                    BicProgressScoreType.KS2, null),
                                Progress8Score = schoolFinancialData.Progress8Measure,
                                Progress8Description = ProgressDescription(null, BicProgressScoreType.P8,
                                    schoolFinancialData.Progress8Measure)
                            };

                            model.ProgressScore.Add(new KeyValuePair<int, ProgressScoreModel>((int)urn, progressModel));
                        }

                        break;
                }

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return NoContent();
            }
        }
    }
}