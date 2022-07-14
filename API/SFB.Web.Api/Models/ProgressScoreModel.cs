﻿using SFB.Web.ApplicationCore.Helpers.Enums;

namespace SFB.Web.Api.Models
{
    public class ProgressScoreModel
    {
        public decimal? Progress8Score { get; set; }
        public string Progress8Description { get; set; }
        public decimal? Ks2Score { get; set; }
        public string Ks2ScoreDescription { get; set; }

    public ProgressScoreModel(){ }
    }
}