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
        public List<SelfAssesmentModel> Academies { get; set; }
        public TrustSelfAssessmentModel()
        {
            
        }
    }
}