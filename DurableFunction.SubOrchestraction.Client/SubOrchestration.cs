using DurableFunction.SubOrchestraction.Client.Constants;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DurableFunction.SubOrchestraction.Client
{
    /// <summary>
    /// SubOrchestration
    /// </summary>
    public static class SubOrchestration
    {
        /// <summary>
        /// Runs the orchestrator.
        /// </summary>
        /// <param name="context">The context.</param>
        [FunctionName(AppConstants.Orchestator)]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var deviceIds = await context.CallActivityAsync<List<string>>(AppConstants.GetNewDeviceIds, null);

            // Run multiple device provisioning flows in parallel
            var provisioningTasks = new List<Task>();
            foreach (string deviceId in deviceIds)
            {
                Task provisionTask = context.CallSubOrchestratorAsync(AppConstants.DeviceProvisioningOrchestration, deviceId);
                provisioningTasks.Add(provisionTask);
            }

            await Task.WhenAll(provisioningTasks);
        }

        /// <summary>
        /// Devices the provisioning orchestration.
        /// </summary>
        /// <param name="context">The context.</param>
        [FunctionName(AppConstants.DeviceProvisioningOrchestration)]
        public static async Task DeviceProvisioningOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            string deviceId = context.GetInput<string>();

            // Step 1: Create an installation package in blob storage and return a SAS URL.
            Uri sasUrl = await context.CallActivityAsync<Uri>(AppConstants.CreateInstallationPackage, deviceId);

            // Step 2: Notify the device that the installation package is ready.
            await context.CallActivityAsync(AppConstants.SendPackageUrlToDevice, Tuple.Create(deviceId, sasUrl));

            // Step 3: Wait for the device to acknowledge that it has downloaded the new package.
            await context.WaitForExternalEvent<bool>(AppConstants.DownloadCompletedAck);
        }

        /// <summary>
        /// Creates the installation package.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="log">The log.</param>
        /// <returns>Uri</returns>
        [FunctionName(AppConstants.CreateInstallationPackage)]
        public static Uri CreateInstallationPackage([ActivityTrigger] string deviceId, ILogger log)
        {
            log.LogInformation($"Processing {deviceId}.");
            return new Uri("https://www.google.com");
        }

        /// <summary>
        /// Sends the package URL to device.
        /// </summary>
        /// <param name="deviceDetails">The device details.</param>
        /// <param name="log">The log.</param>
        [FunctionName(AppConstants.SendPackageUrlToDevice)]
        public static void SendPackageUrlToDevice([ActivityTrigger] Tuple<string, Uri> deviceDetails, ILogger log)
        {
            log.LogInformation($"Processing {deviceDetails.Item1}.");
        }

        /// <summary>
        /// Downloads the completed ack.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <returns>bool</returns>
        [FunctionName(AppConstants.DownloadCompletedAck)]
        public static bool DownloadCompletedAck(ILogger log)
        {
            log.LogInformation($"Processing Download Completed.");
            return true;
        }

        /// <summary>
        /// Gets the new device ids.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="log">The log.</param>
        /// <returns>List of string</returns>
        [FunctionName(AppConstants.GetNewDeviceIds)]
        public static List<string> GetNewDeviceIds([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            var deviceIds = new List<string>();

            for (int i = 1; i <= 100; i++)
            {
                deviceIds.Add($"Device {i}");
            }
            return deviceIds;
        }

        /// <summary>
        /// HTTPs the start.
        /// </summary>
        /// <param name="req">The req.</param>
        /// <param name="starter">The starter.</param>
        /// <param name="log">The log.</param>
        /// <returns>HttpResponseMessage</returns>
        [FunctionName(AppConstants.Client)]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync(AppConstants.Orchestator, null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}