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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Autodesk.Oss;
using Autodesk.SDKManager;
using RevitToIfcScheduler.Models;

namespace RevitToIfcScheduler.Utilities
{
    /// <summary>
    /// Client for APS Design Automation for Revit (v3). Provisions the
    /// RevitIfcExporter appbundle/activity (from the MIT-licensed
    /// https://github.com/ADN-DevTech/aps-revit-ifc-exporter-appbundle)
    /// and submits/polls IFC export workitems.
    /// </summary>
    public static class DesignAutomation
    {
        public const string AppBundleName = "RevitIfcExporter";
        public const string ActivityName = "RevitIfcExporterActivity";
        public const string Alias = "prod";

        private static readonly SDKManager _sdkManager = SdkManagerBuilder.Create().Build();
        private static readonly string _localTempFolder = Path.Combine(Directory.GetCurrentDirectory(), "tmp");

        private static string DaBaseUrl => $"{AppConfig.ApsBaseUrl}/da/us-east/v3";

        // Prebuilt appbundle ZIPs published by ADN-DevTech (MIT license).
        private static readonly Dictionary<string, string> DefaultAppBundleUrls = new()
        {
            ["Autodesk.Revit+2026"] = "https://github.com/ADN-DevTech/aps-revit-ifc-exporter-appbundle/releases/download/v1.2.0/RevitIfcExporter2026.zip",
            ["Autodesk.Revit+2024"] = "https://github.com/ADN-DevTech/aps-revit-ifc-exporter-appbundle/releases/download/v1.1.0/RevitIfcExporter-debug-2024.zip",
            ["Autodesk.Revit+2023"] = "https://github.com/ADN-DevTech/aps-revit-ifc-exporter-appbundle/releases/download/v1.1.0/RevitIfcExporter-debug-2023.zip",
            ["Autodesk.Revit+2022"] = "https://github.com/ADN-DevTech/aps-revit-ifc-exporter-appbundle/releases/download/v1.1.0/RevitIfcExporter-debug-2022.zip",
            ["Autodesk.Revit+2021"] = "https://github.com/ADN-DevTech/aps-revit-ifc-exporter-appbundle/releases/download/v1.1.0/RevitIfcExporter-debug-2021.zip",
        };

        public static async Task<string> GetNicknameAsync(string token)
        {
            var nickname = await $"{DaBaseUrl}/forgeapps/me"
                .WithOAuthBearerToken(token)
                .GetStringAsync();

            return nickname.Trim('"');
        }

        public static string GetQualifiedActivityId(string nickname)
        {
            return $"{nickname}.{ActivityName}+{Alias}";
        }

        /* Provisioning */

        public static async Task<JObject> GetStatusAsync()
        {
            var token = await new TwoLeggedTokenGetter().GetToken();
            var nickname = await GetNicknameAsync(token);

            var appBundleResponse = await $"{DaBaseUrl}/appbundles/{nickname}.{AppBundleName}+{Alias}"
                .WithOAuthBearerToken(token)
                .AllowHttpStatus("404")
                .GetAsync();

            var activityResponse = await $"{DaBaseUrl}/activities/{nickname}.{ActivityName}+{Alias}"
                .WithOAuthBearerToken(token)
                .AllowHttpStatus("404")
                .GetAsync();

            var status = new JObject
            {
                ["nickname"] = nickname,
                ["engine"] = AppConfig.DesignAutomationEngine,
                ["activityId"] = GetQualifiedActivityId(nickname),
                ["appBundleProvisioned"] = appBundleResponse.StatusCode == 200,
                ["activityProvisioned"] = activityResponse.StatusCode == 200,
            };

            if (appBundleResponse.StatusCode == 200)
            {
                var appBundle = JObject.Parse(await appBundleResponse.GetStringAsync());
                status["appBundleEngine"] = appBundle["engine"]?.ToString();
                status["appBundleVersion"] = appBundle["version"]?.ToString();
            }

            if (activityResponse.StatusCode == 200)
            {
                var activity = JObject.Parse(await activityResponse.GetStringAsync());
                status["activityEngine"] = activity["engine"]?.ToString();
                status["activityVersion"] = activity["version"]?.ToString();
            }

            return status;
        }

        public static async Task<JObject> ProvisionAsync()
        {
            var token = await new TwoLeggedTokenGetter().GetToken();
            var nickname = await GetNicknameAsync(token);
            var engine = AppConfig.DesignAutomationEngine;

            var zipPath = await GetAppBundleZipAsync(engine);
            try
            {
                var appBundleVersion = await CreateOrUpdateAppBundleAsync(engine, zipPath, token);
                Log.Information($"Provisioned appbundle {AppBundleName} version {appBundleVersion} for {engine}");

                var activityVersion = await CreateOrUpdateActivityAsync(nickname, engine, token);
                Log.Information($"Provisioned activity {ActivityName} version {activityVersion} for {engine}");
            }
            finally
            {
                if (System.IO.File.Exists(zipPath))
                    System.IO.File.Delete(zipPath);
            }

            return await GetStatusAsync();
        }

        private static async Task<string> GetAppBundleZipAsync(string engine)
        {
            if (!string.IsNullOrWhiteSpace(AppConfig.DesignAutomationAppBundleZipPath))
            {
                if (!System.IO.File.Exists(AppConfig.DesignAutomationAppBundleZipPath))
                    throw new FileNotFoundException($"DesignAutomation:AppBundleZipPath not found: {AppConfig.DesignAutomationAppBundleZipPath}");
                return AppConfig.DesignAutomationAppBundleZipPath;
            }

            var url = AppConfig.DesignAutomationAppBundleZipUrl;
            if (string.IsNullOrWhiteSpace(url) && !DefaultAppBundleUrls.TryGetValue(engine, out url))
                throw new InvalidOperationException(
                    $"No prebuilt appbundle is known for engine '{engine}'. " +
                    "Set DesignAutomation:AppBundleZipUrl or DesignAutomation:AppBundleZipPath in the app settings.");

            Directory.CreateDirectory(_localTempFolder);
            var zipPath = Path.Combine(_localTempFolder, $"appbundle-{Guid.NewGuid()}.zip");
            await using (var download = await url.GetStreamAsync())
            await using (var file = new FileStream(zipPath, FileMode.Create))
            {
                await download.CopyToAsync(file);
            }

            return zipPath;
        }

        private static async Task<int> CreateOrUpdateAppBundleAsync(string engine, string zipPath, string token)
        {
            var spec = new
            {
                id = AppBundleName,
                engine,
                description = "Revit IFC exporter with Revit IFC export options support"
            };

            var createResponse = await $"{DaBaseUrl}/appbundles"
                .WithOAuthBearerToken(token)
                .AllowHttpStatus("409")
                .PostJsonAsync(spec);

            JObject bundle;
            if (createResponse.StatusCode == 409)
            {
                // AppBundle exists: add a new version instead.
                var versionResponse = await $"{DaBaseUrl}/appbundles/{AppBundleName}/versions"
                    .WithOAuthBearerToken(token)
                    .PostJsonAsync(new { engine, description = spec.description });
                bundle = JObject.Parse(await versionResponse.GetStringAsync());
            }
            else
            {
                bundle = JObject.Parse(await createResponse.GetStringAsync());
            }

            var version = bundle["version"].Value<int>();
            var endpointUrl = bundle["uploadParameters"]["endpointURL"].ToString();
            var formData = (JObject)bundle["uploadParameters"]["formData"];

            // S3 policy upload: form fields first, file field last.
            // The file must be added as a seekable stream, not by path: Flurl's path-based
            // AddFile reports an unknown content length, which makes HttpClient fall back to
            // Transfer-Encoding: chunked, and S3 rejects chunked form POSTs with 411.
            await using (var zipStream = System.IO.File.OpenRead(zipPath))
            {
                await endpointUrl.PostMultipartAsync(mp =>
                {
                    foreach (var field in formData.Properties())
                    {
                        mp.AddString(field.Name, field.Value.ToString());
                    }
                    mp.AddFile("file", zipStream, Path.GetFileName(zipPath));
                });
            }

            await CreateOrUpdateAliasAsync($"{DaBaseUrl}/appbundles/{AppBundleName}", version, token);

            return version;
        }

        private static async Task<int> CreateOrUpdateActivityAsync(string nickname, string engine, string token)
        {
            var spec = new
            {
                id = ActivityName,
                commandLine = new[]
                {
                    $"$(engine.path)\\\\revitcoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{AppBundleName}].path)\""
                },
                parameters = new Dictionary<string, object>
                {
                    ["inputFile"] = new { verb = "get", description = "Input Revit File", required = true, localName = "$(inputFile)" },
                    ["userPropertySetsFile"] = new { verb = "get", description = "IFC user defined property set definition file", localName = "userDefinedParameterSets.txt" },
                    ["userParameterMappingFile"] = new { verb = "get", description = "IFC user defined parameter mapping file", localName = "userDefinedParameterMapping.txt" },
                    ["userExportSettingsFile"] = new { verb = "get", description = "JSON-based user-defined IFC export settings file exported from the Revit IFC addin", localName = "userExportSettings.json" },
                    ["inputJson"] = new { verb = "get", required = true, description = "Input JSON parameters", localName = "params.json" },
                    ["outputIFC"] = new { zip = true, verb = "put", description = "Exported IFC files", localName = "ifc" }
                },
                engine,
                appbundles = new[] { $"{nickname}.{AppBundleName}+{Alias}" },
                description = "Activity of Revit IFC Exporter with Autodesk IFC export options support"
            };

            var createResponse = await $"{DaBaseUrl}/activities"
                .WithOAuthBearerToken(token)
                .AllowHttpStatus("409")
                .PostJsonAsync(spec);

            JObject activity;
            if (createResponse.StatusCode == 409)
            {
                var versionResponse = await $"{DaBaseUrl}/activities/{ActivityName}/versions"
                    .WithOAuthBearerToken(token)
                    .PostJsonAsync(spec);
                activity = JObject.Parse(await versionResponse.GetStringAsync());
            }
            else
            {
                activity = JObject.Parse(await createResponse.GetStringAsync());
            }

            var version = activity["version"].Value<int>();
            await CreateOrUpdateAliasAsync($"{DaBaseUrl}/activities/{ActivityName}", version, token);

            return version;
        }

        private static async Task CreateOrUpdateAliasAsync(string resourceUrl, int version, string token)
        {
            var createResponse = await $"{resourceUrl}/aliases"
                .WithOAuthBearerToken(token)
                .AllowHttpStatus("409")
                .PostJsonAsync(new { id = Alias, version });

            if (createResponse.StatusCode == 409)
            {
                await $"{resourceUrl}/aliases/{Alias}"
                    .WithOAuthBearerToken(token)
                    .PatchJsonAsync(new { version });
            }
        }

        /* WorkItem submission */

        public static async Task<string> SubmitWorkItemAsync(ConversionJob conversionJob, IfcSettingsSet settingsSet, string token)
        {
            var nickname = await GetNicknameAsync(token);
            var ossClient = new OssClient(_sdkManager);

            // 1. Input RVT: signed read URL on the file's storage object.
            //    Composite designs were staged as a ZIP in the transient bucket by MoveFileToOss.
            string inputBucketKey;
            string inputObjectKey;
            if (!string.IsNullOrWhiteSpace(conversionJob.InputStorageLocation))
            {
                (inputBucketKey, inputObjectKey) = ParseObjectId(conversionJob.InputStorageLocation);
            }
            else
            {
                var storageId = await APS.GetItemTipStorageId(conversionJob.ProjectId, conversionJob.ItemId, token);
                (inputBucketKey, inputObjectKey) = ParseObjectId(storageId);
            }

            var downloadInfo = await ossClient.SignedS3DownloadAsync(inputBucketKey, inputObjectKey, accessToken: token);
            var inputFileArgument = new Dictionary<string, object>
            {
                ["verb"] = "get",
                ["url"] = downloadInfo.Url
            };
            if (conversionJob.IsCompositeDesign)
            {
                // The staged object is a ZIP; point the exporter at the root model inside it.
                inputFileArgument["localName"] = "inputZip";
                inputFileArgument["pathInZip"] = conversionJob.FileName;
            }

            // 2. Output: the activity zips the "ifc" folder and PUTs it to this URL.
            //    OSS signed URLs are capped at 60 minutes, counted from workitem submission,
            //    so long-running exports would expire them. When PublicHostUrl is configured,
            //    the output is instead PUT to this app's callback endpoint, which never expires.
            var outputObjectName = $"{conversionJob.Id}-ifc.zip";
            string outputUrl;
            if (!string.IsNullOrWhiteSpace(AppConfig.DesignAutomationPublicHostUrl))
            {
                var baseUrl = AppConfig.DesignAutomationPublicHostUrl.TrimEnd('/');
                outputUrl = $"{baseUrl}/api/designAutomation/output/{conversionJob.Id}?token={CreateOutputToken(conversionJob.Id)}";
            }
            else
            {
                outputUrl = await CreateSignedUploadUrlAsync(AppConfig.BucketKey, outputObjectName, token);
                conversionJob.AddLog("Using a signed output URL (60 minute validity). Exports still running ~55 minutes after submission will fail to upload. Set DesignAutomation:PublicHostUrl to remove this limit.");
            }
            conversionJob.OutputStorageLocation = $"urn:adsk.objects:os.object:{AppConfig.BucketKey}/{outputObjectName}";

            // 3. Optional side files from the settings set.
            var arguments = new Dictionary<string, object>
            {
                ["inputFile"] = inputFileArgument,
                ["outputIFC"] = new Dictionary<string, object>
                {
                    ["verb"] = "put",
                    ["url"] = outputUrl,
                    ["headers"] = new Dictionary<string, string> { ["Content-Type"] = "application/octet-stream" }
                }
            };

            var useExportSettingFile = !string.IsNullOrWhiteSpace(settingsSet?.ExportSettingsJson);
            if (useExportSettingFile)
            {
                var settingsUrl = await UploadTextAndSignAsync(ossClient, $"{conversionJob.Id}-settings.json", settingsSet.ExportSettingsJson, token);
                arguments["userExportSettingsFile"] = new Dictionary<string, object> { ["verb"] = "get", ["url"] = settingsUrl };
            }

            var usePsetsFile = !string.IsNullOrWhiteSpace(settingsSet?.UserDefinedPsetsContent);
            if (usePsetsFile)
            {
                var psetsUrl = await UploadTextAndSignAsync(ossClient, $"{conversionJob.Id}-psets.txt", settingsSet.UserDefinedPsetsContent, token);
                arguments["userPropertySetsFile"] = new Dictionary<string, object> { ["verb"] = "get", ["url"] = psetsUrl };
            }

            // 4. params.json, passed inline as a data URL.
            var exportSettingName = conversionJob.IfcSettingsSetName;
            if (useExportSettingFile)
            {
                try
                {
                    var nameInJson = JObject.Parse(settingsSet.ExportSettingsJson)["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(nameInJson)) exportSettingName = nameInJson;
                }
                catch (JsonReaderException)
                {
                    // Fall back to the settings set name; the addin will fail with a clear
                    // message if the JSON itself is malformed.
                }
            }

            var inputParams = new Dictionary<string, object>
            {
                ["exportSettingName"] = exportSettingName,
                ["useExportSettingFile"] = useExportSettingFile,
                ["onlyExportVisibleElementsInView"] = settingsSet?.OnlyExportVisibleElementsInView ?? false
            };
            if (!string.IsNullOrWhiteSpace(settingsSet?.ViewId))
                inputParams["viewId"] = settingsSet.ViewId;
            if (usePsetsFile)
                inputParams["userDefinedPropertySetsFilenameOverride"] = "userDefinedParameterSets.txt";

            arguments["inputJson"] = new Dictionary<string, object>
            {
                ["verb"] = "get",
                ["url"] = "data:application/json," + JsonConvert.SerializeObject(inputParams)
            };

            var workItemSpec = new
            {
                activityId = GetQualifiedActivityId(nickname),
                arguments
            };

            conversionJob.AddLog($"WorkItem params: {JsonConvert.SerializeObject(inputParams)}");

            var response = await $"{DaBaseUrl}/workitems"
                .WithOAuthBearerToken(token)
                .PostJsonAsync(workItemSpec);

            var workItem = JObject.Parse(await response.GetStringAsync());
            return workItem["id"].ToString();
        }

        public static async Task<JObject> GetWorkItemStatusAsync(string workItemId, string token)
        {
            var response = await $"{DaBaseUrl}/workitems/{workItemId}"
                .WithOAuthBearerToken(token)
                .GetStringAsync();

            return JObject.Parse(response);
        }

        public static async Task<string> GetWorkItemReportAsync(string reportUrl)
        {
            try
            {
                var report = await reportUrl.GetStringAsync();
                // Reports can be long; keep the tail, which contains the failure.
                const int maxLength = 4000;
                return report.Length <= maxLength ? report : report.Substring(report.Length - maxLength);
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not fetch workitem report: {exception.Message}");
                return null;
            }
        }

        /* Output retrieval */

        public static async Task<string> DownloadOutputAndExtractIfc(ConversionJob conversionJob, string token)
        {
            var ossClient = new OssClient(_sdkManager);
            var outputObjectName = $"{conversionJob.Id}-ifc.zip";
            Directory.CreateDirectory(_localTempFolder);
            var zipPath = Path.Combine(_localTempFolder, outputObjectName);
            var extractPath = Path.Combine(_localTempFolder, $"{conversionJob.Id}-ifc");

            await ossClient.Download(AppConfig.BucketKey, outputObjectName, zipPath, token, CancellationToken.None);

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            ZipFile.ExtractToDirectory(zipPath, extractPath);
            System.IO.File.Delete(zipPath);

            string[] ifcExtensions = [".ifc", ".ifczip", ".ifcxml"];
            var ifcFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories)
                .Where(path => ifcExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .ToList();

            if (ifcFiles.Count == 0)
                throw new InvalidOperationException("Design Automation output did not contain an IFC file.");

            if (ifcFiles.Count > 1)
                Log.Warning($"Workitem for job {conversionJob.Id} produced {ifcFiles.Count} IFC files; using {ifcFiles.First()}");

            return ifcFiles.First();
        }

        public static void CleanupOutput(ConversionJob conversionJob)
        {
            var extractPath = Path.Combine(_localTempFolder, $"{conversionJob.Id}-ifc");
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
        }

        /* Output callback (no-expiry alternative to signed output URLs) */

        public static string CreateOutputToken(Guid conversionJobId)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AppConfig.ClientSecret));
            var hash = hmac.ComputeHash(conversionJobId.ToByteArray());
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static bool ValidateOutputToken(Guid conversionJobId, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var expected = Encoding.UTF8.GetBytes(CreateOutputToken(conversionJobId));
            var provided = Encoding.UTF8.GetBytes(token.ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(expected, provided);
        }

        /// <summary>
        /// Receives the output ZIP that Design Automation PUTs to the callback
        /// endpoint and stores it as the job's output object in the transient
        /// bucket, where the poller picks it up on workitem success.
        /// </summary>
        public static async Task ReceiveOutputAsync(Guid conversionJobId, Stream body)
        {
            var outputObjectName = $"{conversionJobId}-ifc.zip";
            Directory.CreateDirectory(_localTempFolder);
            var tempPath = Path.Combine(_localTempFolder, $"{conversionJobId}-callback.zip");

            await using (var file = new FileStream(tempPath, FileMode.Create))
            {
                await body.CopyToAsync(file);
            }

            try
            {
                var token = await new TwoLeggedTokenGetter().GetToken();
                var ossClient = new OssClient(_sdkManager);
                await ossClient.Upload(AppConfig.BucketKey, outputObjectName, tempPath, token, CancellationToken.None);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        /* Helpers */

        public static (string bucketKey, string objectKey) ParseObjectId(string objectId)
        {
            var results = objectId.Replace("urn:adsk.objects:os.object:", string.Empty).Split('/');
            if (results.Length < 2)
                throw new InvalidOperationException($"Could not parse OSS object id: {objectId}");
            return (results[0], results[1]);
        }

        private static async Task<string> CreateSignedUploadUrlAsync(string bucketKey, string objectName, string token)
        {
            var response = await $"{AppConfig.ApsBaseUrl}/oss/v2/buckets/{bucketKey}/objects/{objectName}/signed?access=readwrite"
                .WithOAuthBearerToken(token)
                .PostJsonAsync(new { minutesExpiration = 60, singleUse = false });

            var data = JObject.Parse(await response.GetStringAsync());
            return data["signedUrl"].ToString();
        }

        private static async Task<string> UploadTextAndSignAsync(OssClient ossClient, string objectName, string content, string token)
        {
            Directory.CreateDirectory(_localTempFolder);
            var filePath = Path.Combine(_localTempFolder, objectName);
            await System.IO.File.WriteAllTextAsync(filePath, content);
            try
            {
                await ossClient.Upload(AppConfig.BucketKey, objectName, filePath, token, CancellationToken.None);
            }
            finally
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            var downloadInfo = await ossClient.SignedS3DownloadAsync(AppConfig.BucketKey, objectName, accessToken: token);
            return downloadInfo.Url;
        }
    }
}
