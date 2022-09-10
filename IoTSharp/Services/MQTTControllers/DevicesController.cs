﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using MQTTnet.AspNetCore.AttributeRouting;
using DotNetCore.CAP;
using EasyCaching.Core;
using IoTSharp.FlowRuleEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MQTTnet.Server;
using IoTSharp.Data;
using Dynamitey.DynamicObjects;
using Amazon.SimpleNotificationService.Model;
using System.Collections.Generic;
using MQTTnet;
using IoTSharp.Extensions;
using NATS.Client;
using static IronPython.Modules._ast;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace IoTSharp.Services.MQTTControllers
{
    [MqttController]
    [MqttRoute("[controller]")]
    public class DevicesController : MqttBaseController
    {
        readonly ILogger _logger;
        private readonly IServiceScopeFactory _scopeFactor;
        private readonly IEasyCachingProviderFactory _factory;
        readonly MqttServer _serverEx;
        private readonly ICapPublisher _queue;
        private readonly FlowRuleProcessor _flowRuleProcessor;
        private readonly IEasyCachingProvider _caching;
        private readonly Device _dev;
        private readonly MQTTService _service;
        readonly MqttClientSetting _mcsetting;
        private readonly AppSettings _settings;

        public DevicesController(ILogger<DevicesController> logger, IServiceScopeFactory scopeFactor, MQTTService mqttService,
            IOptions<AppSettings> options, ICapPublisher queue, IEasyCachingProviderFactory factory, FlowRuleProcessor flowRuleProcessor
            )
        {
            string _hc_Caching = $"{nameof(CachingUseIn)}-{Enum.GetName(options.Value.CachingUseIn)}";
            _mcsetting = options.Value.MqttClient;
            _settings = options.Value;
            _logger = logger;
            _scopeFactor = scopeFactor;
            _factory = factory;
            _queue = queue;
            _flowRuleProcessor = flowRuleProcessor;
            _caching = factory.GetCachingProvider(_hc_Caching);
            _dev  =Lazy.Create(async ()=>await GetSessionDataAsync<Device>(nameof(Device)));
            _service = mqttService;
        }

        [MqttRoute("{devname}/telemetry/xml/{keyname}")]
        public Task telemetry_xml(string devname,string keyname)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
                var xml = new System.Xml.XmlDocument();
                xml.LoadXml(Message.ConvertPayloadToString());
                keyValues.Add(keyname, xml);
                _queue.PublishTelemetryData(device, keyValues);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
            return Ok();
        }
        [MqttRoute("{devname}/telemetry/binary/{keyname}")]
        public Task telemetry_binary(string devname, string keyname)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
              
                keyValues.Add(keyname, Message.Payload);
                _queue.PublishTelemetryData(device, keyValues);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
            return Ok();
        }


        [MqttRoute("{devname}/attributes/xml/{keyname}")]
        public Task attributes_xml(string devname, string keyname)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
                var xml = new System.Xml.XmlDocument();
                xml.LoadXml(Message.ConvertPayloadToString());
                keyValues.Add(keyname, xml);
                _queue.PublishAttributeData(device, keyValues);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
            return Ok();
        }


        [MqttRoute("{devname}/attributes/binary/{keyname}")]
        public Task attributes_binary(string devname, string keyname)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {

                keyValues.Add(keyname, Message.Payload);
                _queue.PublishAttributeData(device, keyValues);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
            return Ok();
        }

        // Supports template routing with typed constraints
        [MqttRoute("{devname}/attributes")]
        public Task attributes(string devname)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
                if (Message.Payload?.Length > 0)
                {
                    keyValues = Message.ConvertPayloadToDictionary();
                    _queue.PublishAttributeData(device, keyValues);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
            return Ok();
        }
        [MqttRoute("{devname}/telemetry")]
        public Task telemetry(string devname)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
                if (Message.Payload?.Length > 0)
                {
                    keyValues = Message.ConvertPayloadToDictionary();
                    _queue.PublishAttributeData(device, keyValues);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
            return Ok();
        }
        [MqttRoute("{devname}/attributes/request/{keyname}/{requestid}/xml")]
        public async Task RequestAttributes(string devname, string keyname, string requestid)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
                using (var scope = _scopeFactor.CreateScope())
                using (var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    var qf = from at in _dbContext.AttributeLatest where at.Type == DataType.XML && at.KeyName == keyname select at;
                    await qf.LoadAsync();
                    await Server.PublishAsync(ClientId, $"devices/me/attributes/response/{requestid}", qf.FirstOrDefault()?.Value_XML);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
        }

        [MqttRoute("{devname}/attributes/request/{keyname}/{requestid}/binary")]
        public async Task RequestAttributes_binary(string devname, string keyname, string requestid)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
                using (var scope = _scopeFactor.CreateScope())
                using (var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    var qf = from at in _dbContext.AttributeLatest where at.Type == DataType.Binary && at.KeyName == keyname select at;
                    await qf.LoadAsync();
                    await Server.PublishAsync(ClientId, $"devices/me/attributes/response/{requestid}", qf.FirstOrDefault()?.Value_Binary);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
        }

        [MqttRoute("{devname}/attributes/request/{requestid}")]
        public async Task RequestAttributes(string devname,string requestid)
        {
            var device = _dev.JudgeOrCreateNewDevice(devname, _scopeFactor, _logger);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            try
            {
                using (var scope = _scopeFactor.CreateScope())
                using (var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                        Dictionary<string, object> reps = new Dictionary<string, object>();
                        var reqid = requestid;
                        List<AttributeLatest> datas = new List<AttributeLatest>();
                        foreach (var kx in keyValues)
                        {
                            var keys = kx.Value?.ToString().Split(',');
                            if (keys != null && keys.Length > 0)
                            {
                                if (Enum.TryParse(kx.Key, true, out DataSide ds))
                                {
                                    var qf = from at in _dbContext.AttributeLatest where at.DeviceId == device.Id && keys.Contains(at.KeyName) select at;
                                    await qf.LoadAsync();
                                    if (ds == DataSide.AnySide)
                                    {
                                        datas.AddRange(await qf.ToArrayAsync());
                                    }
                                    else
                                    {
                                        var qx = from at in qf where at.DataSide == ds select at;
                                        datas.AddRange(await qx.ToArrayAsync());
                                    }
                                }
                            }
                        }


                        foreach (var item in datas)
                        {
                            switch (item.Type)
                            {
                                case DataType.Boolean:
                                    reps.Add(item.KeyName, item.Value_Boolean);
                                    break;
                                case DataType.String:
                                    reps.Add(item.KeyName, item.Value_String);
                                    break;
                                case DataType.Long:
                                    reps.Add(item.KeyName, item.Value_Long);
                                    break;
                                case DataType.Double:
                                    reps.Add(item.KeyName, item.Value_Double);
                                    break;
                                case DataType.Json:
                                    reps.Add(item.KeyName, Newtonsoft.Json.Linq.JToken.Parse(item.Value_Json));
                                    break;
                                case DataType.XML:
                                    reps.Add(item.KeyName, item.Value_XML);
                                    break;
                                case DataType.Binary:
                                    reps.Add(item.KeyName, item.Value_Binary);
                                    break;
                                case DataType.DateTime:
                                    reps.Add(item.KeyName, item.Value_DateTime);
                                    break;
                                default:
                                    reps.Add(item.KeyName, item.Value_Json);
                                    break;
                            }
                        await Server.PublishAsync(ClientId, $"devices/me/attributes/response/{requestid}", reps);
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{ex.Message}");
            }
    
        }



    }
}
