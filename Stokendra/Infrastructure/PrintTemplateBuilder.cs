using System;
using System.Text;

namespace Stokendra.Infrastructure;

public static class PrintTemplateBuilder
{
    public static string BuildReportHtml(
        string title, 
        string headerTitle, 
        string dateInfo, 
        string tableHeadersHtml, 
        string tableBodyHtml, 
        string footerHtml,
        string additionalSummaryHtml = "")
    {
        var sb = new StringBuilder();
        sb.Append($"<html><head><meta charset='utf-8'><title>{title}</title>");
        sb.Append("<style>");
        sb.Append("@page { size: landscape; margin: 0.5cm; } ");
        sb.Append("body { font-family: 'Segoe UI', Arial, sans-serif; padding: 10px; color: #1a1a1a; line-height: 1.2; } ");
        sb.Append(".top-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #2563EB; padding-bottom: 8px; margin-bottom: 15px; } ");
        sb.Append(".top-header h1 { margin: 0; color: #2563EB; font-size: 20px; font-weight: 800; } ");
        sb.Append(".date-box { text-align: right; font-size: 11px; color: #4b5563; } ");
        
        sb.Append(".summary { display: flex; justify-content: space-between; margin-bottom: 20px; background: #f1f5f9; padding: 15px; border-radius: 8px; } ");
        sb.Append(".summary-item { text-align: center; flex: 1; } ");
        sb.Append(".summary-value { font-size: 20px; font-weight: bold; color: #1e293b; } ");
        sb.Append(".summary-label { font-size: 11px; color: #64748b; text-transform: uppercase; } ");
        
        sb.Append("table { width: 100%; border-collapse: collapse; font-size: 11px; table-layout: fixed; } ");
        sb.Append("th, td { border: 1px solid #666; padding: 6px 4px; word-wrap: break-word; white-space: normal; } ");
        sb.Append("th { background: #f1f5f9; font-weight: bold; text-align: center; text-transform: uppercase; } ");
        sb.Append(".num { text-align: center; } ");
        sb.Append(".bold { font-weight: bold; } ");
        sb.Append(".green { color: #10B981; } ");
        sb.Append(".red { color: #EF4444; } ");
        
        sb.Append(".footer-row { background: #f8fafc; font-weight: 800; } ");
        sb.Append(".footer-row td { border-top: 2px solid #1e293b; font-size: 12px; } ");
        sb.Append(".footer { margin-top: 20px; font-size: 10px; text-align: right; color: #94a3b8; } ");
        
        sb.Append("@media print { body { -webkit-print-color-adjust: exact; } table { page-break-inside: auto; } tr { page-break-inside: avoid; page-break-after: auto; } } ");
        sb.Append("</style>");
        sb.Append("<script>window.onload = function() { window.print(); }</script>");
        sb.Append("</head><body>");
        
        sb.Append("<div class='top-header'>");
        sb.Append($"<h1>{headerTitle}</h1>");
        sb.Append($"<div class='date-box'>{dateInfo}</div>");
        sb.Append("</div>");

        if (!string.IsNullOrEmpty(additionalSummaryHtml))
        {
            sb.Append(additionalSummaryHtml);
        }

        sb.Append("<table><thead><tr>");
        sb.Append(tableHeadersHtml);
        sb.Append("</tr></thead><tbody>");
        
        sb.Append(tableBodyHtml);
        
        sb.Append("</tbody></table>");
        sb.Append($"<div class='footer'>{footerHtml}</div>");
        sb.Append("</body></html>");
        
        return sb.ToString();
    }
}
