/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Developer Advocacy and Support
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;
using Hangfire;
using Serilog;
using RevitToIfcScheduler.Models;

namespace RevitToIfcScheduler.Utilities
{
    public class HangfireJobs
    {
        private readonly Context.RevitIfcContext _revitIfcContext;
        public HangfireJobs(Context.RevitIfcContext revitIfcContext)
        {
            _revitIfcContext = revitIfcContext;
        }

        public async Task PollConversionJob(Guid conversionJobId)
        {
            try
            {
                var conversionJob = await _revitIfcContext.ConversionJobs.FindAsync(conversionJobId);

                if (string.IsNullOrWhiteSpace(conversionJob.WorkItemId))
                {
                    conversionJob.Status = ConversionJobStatus.Failed;
                    conversionJob.AddLog("No Design Automation WorkItem id on this job");
                    _revitIfcContext.ConversionJobs.Update(conversionJob);
                    await _revitIfcContext.SaveChangesAsync();
                    return;
                }

                var token = await new TwoLeggedTokenGetter().GetToken();
                var workItem = await DesignAutomation.GetWorkItemStatusAsync(conversionJob.WorkItemId, token);
                var status = workItem["status"]?.ToString();
                var reportUrl = workItem["reportUrl"]?.ToString();

                if (!string.IsNullOrWhiteSpace(reportUrl))
                {
                    conversionJob.WorkItemReportUrl = reportUrl;
                }

                switch (status)
                {
                    case "pending":
                    case "inprogress":
                        conversionJob.AddLog($"Design Automation WorkItem: {status}");
                        BackgroundJob.Schedule<HangfireJobs>(x => x.PollConversionJob(conversionJob.Id), TimeSpan.FromMinutes(1));
                        break;
                    case "success":
                        conversionJob.AddLog("Design Automation WorkItem succeeded");
                        _revitIfcContext.ConversionJobs.Update(conversionJob);
                        await _revitIfcContext.SaveChangesAsync();

                        //Deliver the IFC output to the ACC/BIM360 folder
                        await ConversionJob.OnReceive(conversionJob);
                        break;
                    case "cancelled":
                        conversionJob.Status = ConversionJobStatus.Failed;
                        conversionJob.JobFinished = DateTime.UtcNow;
                        conversionJob.AddLog("Conversion Cancelled");
                        await AppendWorkItemReport(conversionJob, reportUrl);
                        break;
                    case "failedLimitProcessingTime":
                        conversionJob.Status = ConversionJobStatus.TimeOut;
                        conversionJob.JobFinished = DateTime.UtcNow;
                        conversionJob.AddLog("Conversion Timed Out (Design Automation processing time limit)");
                        await AppendWorkItemReport(conversionJob, reportUrl);
                        break;
                    default:
                        //failedDownload, failedInstructions, failedUpload, failedLimitDataSize, ...
                        conversionJob.Status = ConversionJobStatus.Failed;
                        conversionJob.JobFinished = DateTime.UtcNow;
                        conversionJob.AddLog($"Conversion Failed: {status}");
                        await AppendWorkItemReport(conversionJob, reportUrl);
                        break;
                }

                _revitIfcContext.ConversionJobs.Update(conversionJob);
                await _revitIfcContext.SaveChangesAsync();

            }
            catch (Exception exception)
            {
                Log.Error(exception.Message);
                Log.Error(exception.StackTrace);
                throw;
            }
        }

        private static async Task AppendWorkItemReport(ConversionJob conversionJob, string reportUrl)
        {
            if (string.IsNullOrWhiteSpace(reportUrl)) return;

            var report = await DesignAutomation.GetWorkItemReportAsync(reportUrl);
            if (!string.IsNullOrWhiteSpace(report))
            {
                conversionJob.AddLog($"WorkItem report (tail):\n{report}");
            }
        }
    }
}
