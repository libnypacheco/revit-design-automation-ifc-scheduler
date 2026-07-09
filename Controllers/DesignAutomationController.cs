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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using RevitToIfcScheduler.Models;
using RevitToIfcScheduler.Utilities;

namespace RevitToIfcScheduler.Controllers
{
    public class DesignAutomationController : ControllerBase
    {
        public DesignAutomationController(Context.RevitIfcContext revitIfcContext)
        {
            RevitIfcContext = revitIfcContext;
        }

        private Context.RevitIfcContext RevitIfcContext { get; set; }

        [HttpGet]
        [Route("api/designAutomation/status")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetStatus()
        {
            try
            {
                if (!Authentication.IsAuthorized(HttpContext, RevitIfcContext)) return Unauthorized();

                var status = await DesignAutomation.GetStatusAsync();
                return Ok(status.ToString());
            }
            catch (Exception ex)
            {
                Log.Debug(ex, this.GetType().FullName);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Callback target for Design Automation output uploads. Authenticated by a
        /// per-job HMAC token instead of a user session, since the caller is the
        /// Design Automation service. Unlike OSS signed URLs (60 minute maximum),
        /// this endpoint does not expire, so long-running exports can deliver.
        /// </summary>
        [HttpPut]
        [Route("api/designAutomation/output/{conversionJobId}")]
        [DisableRequestSizeLimit]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> ReceiveOutput(Guid conversionJobId, [FromQuery] string token)
        {
            try
            {
                if (!DesignAutomation.ValidateOutputToken(conversionJobId, token)) return Unauthorized();

                var conversionJob = await RevitIfcContext.ConversionJobs.FindAsync(conversionJobId);
                if (conversionJob == null) return NotFound(conversionJobId);

                await DesignAutomation.ReceiveOutputAsync(conversionJobId, Request.Body);

                conversionJob.AddLog("Received Design Automation output via callback");
                RevitIfcContext.ConversionJobs.Update(conversionJob);
                await RevitIfcContext.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to receive Design Automation output");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("api/designAutomation/provision")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Provision()
        {
            try
            {
                if (!Authentication.IsAuthorized(HttpContext, RevitIfcContext, new List<AccountRole>() { AccountRole.AccountAdmin, AccountRole.ApplicationAdmin })) return Unauthorized();

                var status = await DesignAutomation.ProvisionAsync();
                return Ok(status.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Design Automation provisioning failed");
                return BadRequest(ex.Message);
            }
        }
    }
}
