﻿namespace Kenc.AbuseIPDB.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Kenc.AbuseIPDB.Exceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory(TestConstants.IntegrationTests)]
    [TestCategory("SkipWhenLiveUnitTesting")]
    public class IntegrationTests
    {
        private static IAbuseIPDBClient client;
        private static TestContext context;

        [ClassInitialize]
        public static void ClassInitialiser(TestContext context)
        {
            IntegrationTests.context = context;
            var apiKey = ReadProperty("apiKey");

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("API Key not specified. Cannot run tests");
            }
            client = new AbuseIPDBClient(httpClient: new HttpClient(), apiKey, AbuseIpDbEndpoints.V2Endpoint);
        }

        private static string ReadProperty(string key)
        {
            return context.Properties.Contains(key) ?
                (string)context.Properties[key] :
                Environment.GetEnvironmentVariable($"testsettings-{key}");
        }

        [TestMethod]
        public async Task HandleErrorsCorrectly()
        {
            static async Task action() =>
                await client.ReportAsync("127.0.0.2", "Validation testing report endpoint", new[] { Category.BadWebBot });

            ApiException exception = await Assert.ThrowsExceptionAsync<ApiException>(action);

            ApiReplies.Error firstError = exception.Errors[0];
            firstError.Status.Should().Be(HttpStatusCode.Forbidden);
            firstError.Source.Parameter.Should().Be("ip");
            firstError.Detail.Should().Be("You can only report the same IP address (`127.0.0.2`) once in 15 minutes.");
        }

        [TestMethod]
        public async Task DoIPLookup()
        {
            var ip = ReadProperty("ipaddress");
            (Entities.Check data, RateLimit _) = await client.CheckAsync(ip, 90, false, CancellationToken.None);

            data.IpAddress.Should().Be(ip);
            data.CountryCode.Should().Be("US");
        }

        [TestMethod]
        public async Task DoCheckBlock()
        {
            (Entities.CheckBlockData data, RateLimit _) = await client.CheckBlockAsync("127.0.0.1/24");
            data.NetworkAddress.Should().Be("127.0.0.1");
            data.Netmask.Should().Be("255.255.255.0");
            data.MinAddress.Should().Be("127.0.0.1");
            data.MaxAddress.Should().Be("127.0.0.254");
            data.NumPossibleHosts.Should().Be(254);
            data.AddressSpaceDesc.Should().Be("Loopback");
        }
    }
}
