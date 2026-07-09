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
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace RevitToIfcScheduler.Models
{
    public class IfcSettingsSet
    {
        [Key]
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; }

        /// <summary>
        /// Content of an IFC export setup JSON exported from Revit's IFC export
        /// dialog ("Save selected setup"). When present, the conversion passes it
        /// to Design Automation as userExportSettings.json, so the setup does not
        /// need to be saved inside the Revit model.
        /// </summary>
        [JsonProperty("exportSettingsJson")]
        public string ExportSettingsJson { get; set; }

        /// <summary>
        /// Revit UniqueId of the 3D view to export from. Note that UniqueIds are
        /// per-model, so this is only useful for settings sets dedicated to a
        /// single model.
        /// </summary>
        [JsonProperty("viewId")]
        public string ViewId { get; set; }

        [JsonProperty("onlyExportVisibleElementsInView")]
        public bool OnlyExportVisibleElementsInView { get; set; }

        /// <summary>
        /// Content of a user-defined property sets definition (.txt) file. When
        /// present it is passed to Design Automation, replacing any file path
        /// baked into the export setup (which would be unreachable in the cloud).
        /// </summary>
        [JsonProperty("userDefinedPsetsContent")]
        public string UserDefinedPsetsContent { get; set; }
    }
}