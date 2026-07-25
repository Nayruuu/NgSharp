using System;
using System.Collections.Generic;

namespace NgSharp.Benchmark.Realistic;

public sealed class Quote
{
    public string Number { get; set; }

    public string Reference { get; set; }

    public string CampaignName { get; set; }

    public DateTime IssueDate { get; set; }

    public DateTime ValidUntil { get; set; }

    public DateTime CampaignStart { get; set; }

    public DateTime CampaignEnd { get; set; }

    public Party Issuer { get; set; }

    public Party Client { get; set; }

    public List<QuoteSection> Sections { get; set; }

    public int TotalImpressions { get; set; }

    public decimal TotalHT { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TvaRate { get; set; }

    public decimal TotalTva { get; set; }

    public decimal TotalTTC { get; set; }

    public bool HasOptions { get; set; }

    public List<TextItem> Options { get; set; }

    public List<TextItem> Terms { get; set; }

    public string Notes { get; set; }
}
