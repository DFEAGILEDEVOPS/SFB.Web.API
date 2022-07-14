using System.Collections.Generic;
using SFB.Web.ApplicationCore.Entities;
using SFB.Web.ApplicationCore.Models;

namespace SFB.Web.Api.Models
{
    public class TrustSelfAssessmentModel
    {
        public string TrustName { get; set; }
        public long Uid { get; set; }
        public int? CompanyNumber { get; set; }
        
        public List<EdubaseDataObject> Academies { get; set; }
        public SADSizeLookupDataObject SadSizeLookup { get; set; }
        public SADFSMLookupDataObject SadFSMLookup { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> InYearBalance { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> RevenueReserve { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> TeachingStaff { get; set; }

        public List<KeyValuePair<int, SadAssesmentAreaModel>> SupplyStaff { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> EducationSupportStaff { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> AdminAndClericalStaff { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> OtherStaffCosts { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> PremisesCosts { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> EducationalSupplies { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> Energy { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> AverageTeacherCost { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> SeniorLeaders { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> PupilToTeacherRatio { get; set; }
        public List<KeyValuePair<int, SadAssesmentAreaModel>> PupilToAdultRatio { get; set; }
        public List<KeyValuePair<int, ProgressScoreModel>> ProgressScore { get; set; }

        public TrustSelfAssessmentModel()
        {
            
        }
    }
}