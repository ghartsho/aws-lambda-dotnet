﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.WebTester.Models;
using Amazon.Lambda.TestTool.WebTester.SampleRequests;
using LitJson;
using Microsoft.AspNetCore.Mvc;


namespace Amazon.Lambda.TestTool.WebTester.Controllers
{
    [Route("webtester-api/[controller]")]
    public class TesterController : Controller
    {
        public LocalLambdaOptions LambdaOptions { get; set; }

        public TesterController(LocalLambdaOptions lambdaOptions)
        {
            this.LambdaOptions = lambdaOptions;
        }

        [HttpGet("{configFile}")]
        public ConfigFileSummary GetFunctions(string configFile)
        {
            var fullConfigFilePath = this.LambdaOptions.LambdaConfigFiles.FirstOrDefault(x =>
                string.Equals(configFile, Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
            if (fullConfigFilePath == null)
            {
                throw new Exception($"{configFile} is not a config file for this project");
            }
            
            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(fullConfigFilePath);
            var functions = this.LambdaOptions.LambdaRuntime.LoadLambdaFunctions(configInfo.FunctionInfos);

            var summary = new ConfigFileSummary
            {
                AWSProfile = configInfo.AWSProfile,
                AWSRegion = configInfo.AWSRegion,
                Functions = new List<FunctionSummary>()
            };
            
            foreach (var function in functions)
            {
                summary.Functions.Add(new FunctionSummary()
                {
                    FunctionName = function.FunctionInfo.Name,
                    FunctionHandler = function.FunctionInfo.Handler,
                    ErrorMessage =  function.ErrorMessage
                });                
            }

            return summary;
        }

        [HttpPost("{configFile}/{functionHandler}")]
        public ExecutionResponse ExecuteFunction(string configFile, string functionHandler)
        {
            var function = this.LambdaOptions.LoadLambdaFuntion(configFile, functionHandler);
            string functionInvokeParams = null;
            if (this.Request.ContentLength > 0)
            {
                using (var reader = new StreamReader(this.Request.Body))
                {
                    functionInvokeParams = reader.ReadToEnd();
                }
            }

            var request = new ExecutionRequest()
            {
                Function = function
            };

            if (!string.IsNullOrEmpty(functionInvokeParams))
            {
                var rootData = JsonMapper.ToObject(functionInvokeParams);

                if (rootData.ContainsKey("payload"))
                    request.Payload = rootData["payload"]?.ToString();
                if (rootData.ContainsKey("profile"))
                    request.AWSProfile = rootData["profile"]?.ToString();
                if (rootData.ContainsKey("region"))
                    request.AWSRegion = rootData["region"]?.ToString();
            }
            



            var response = this.LambdaOptions.LambdaRuntime.ExecuteLambdaFunction(request);
            return response;
        }

        [HttpGet("request/{requestName}")]
        public string GetLambdaRequest(string requestName)
        {
            var manager = new SampleRequestManager(this.LambdaOptions.PreferenceDirectory);
            return manager.GetRequest(requestName);
        }

        [HttpPost("request/{requestName}")]
        public string SaveLambdaRequest(string requestName)
        {
            using (var reader = new StreamReader(this.Request.Body))
            {
                var content = reader.ReadToEnd();
                var manager = new SampleRequestManager(this.LambdaOptions.PreferenceDirectory);
                return manager.SaveRequest(requestName, content);
            }
        }
    }
}
