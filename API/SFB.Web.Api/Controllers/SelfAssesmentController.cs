﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Helpers.Enums;
using SFB.Web.ApplicationCore.Models;
using SFB.Web.ApplicationCore.Services.DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
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
                var schoolContextData = await _contextDataService.GetSchoolDataObjectByUrnAsync(urn);//TODO: Do we need all the context data?
                var financeType = (EstablishmentType)Enum.Parse(typeof(EstablishmentType), schoolContextData.FinanceType);

                selfAssesmentModel = await BuildSelfAssesmentModel(urn, financeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            return selfAssesmentModel;
        }

        private async Task<SelfAssesmentModel> BuildSelfAssesmentModel(int urn, EstablishmentType financeType)
        {
            string termYears = await GetTermYears(financeType);
            var schoolFinancialData = await _financialDataService.GetSchoolFinancialDataObjectAsync(urn, financeType, CentralFinancingType.Include);
            var model = new SelfAssesmentModel(
                urn, 
                schoolFinancialData.SchoolName, 
                schoolFinancialData.OverallPhase, 
                schoolFinancialData.LondonWeight, 
                schoolFinancialData.NoPupils.GetValueOrDefault(), 
                schoolFinancialData.PercentageFSM.GetValueOrDefault(), 
                bool.Parse(schoolFinancialData.Has6Form), 
                termYears);
            
            model.SadSizeLookup = await _selfAssesmentDashboardDataService.GetSADSizeLookupDataObject(schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.NoPupils.GetValueOrDefault(), termYears);
            
            model.SadFSMLookup = await _selfAssesmentDashboardDataService.GetSADFSMLookupDataObject(schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.PercentageFSM.GetValueOrDefault(), termYears);

            model.SadAssesmentAreas = new List<SadAssesmentAreaModel>();

            await AddAssessmentArea("Spending", "Teaching staff", financeType, schoolFinancialData.TeachingStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Supply staff", financeType, schoolFinancialData.SupplyStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Education support staff", financeType, schoolFinancialData.EducationSupportStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Administrative and clerical staff", financeType, schoolFinancialData.AdministrativeClericalStaff.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Other staff costs", financeType, schoolFinancialData.OtherStaffCosts.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Premises costs", financeType, schoolFinancialData.Premises.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Teaching resources", financeType, schoolFinancialData.EducationalSupplies.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Spending", "Energy", financeType, schoolFinancialData.Energy.GetValueOrDefault(), schoolFinancialData.TotalExpenditure.GetValueOrDefault(), schoolFinancialData, model, termYears);
            
            await AddAssessmentArea("Reserve and balance", "In-year balance", financeType, schoolFinancialData.InYearBalance.GetValueOrDefault(), schoolFinancialData.TotalIncome.GetValueOrDefault(), schoolFinancialData, model, termYears);
            await AddAssessmentArea("Reserve and balance", "Revenue reserve", financeType, schoolFinancialData.RevenueReserve.GetValueOrDefault(), schoolFinancialData.TotalIncome.GetValueOrDefault(), schoolFinancialData, model, termYears);

            return model;
        }

        private async Task AddAssessmentArea(string areaType, string areaName, EstablishmentType financeType, decimal schoolData, decimal total, SchoolTrustFinancialDataObject schoolFinancialData, SelfAssesmentModel model, string termYears)
        {
            List<SADSchoolRatingsDataObject> teachingStaffTresholds = await _selfAssesmentDashboardDataService.GetSADSchoolRatingsDataObjectAsync(areaName, financeType, schoolFinancialData.OverallPhase, bool.Parse(schoolFinancialData.Has6Form), schoolFinancialData.LondonWeight, model.SadSizeLookup?.SizeType, model.SadFSMLookup?.FSMScale, schoolData / total, termYears);
            teachingStaffTresholds = teachingStaffTresholds.OrderBy(t => t.ScoreLow).ToList();
            model.SadAssesmentAreas.Add(new SadAssesmentAreaModel(areaType, areaName, schoolData, schoolData / total, teachingStaffTresholds));
        }

        private async Task<string> GetTermYears(EstablishmentType financeType)
        {
            var term = await _financialDataService.GetLatestDataYearPerEstabTypeAsync(financeType);
            var termYears = $"{term - 1}/{term}";
            return termYears;
        }
    }
}