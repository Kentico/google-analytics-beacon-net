using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GaBeacon.Controllers
{
    [Route("api")]
    [ApiController]
    public class BeaconController : ControllerBase
    {
        private const string GA_URI = "http://www.google-analytics.com/collect";
        private const string TRACKING_ID = "tid";
        private const string CLIENT_ID = "cid";
        private const string HIT_TYPE = "t";
        private const string PAGE_PATH = "dp";
        private const string IP_ADDRESS = "uip";
        private const string USE_REFERER = "useReferer";
        private const string COOKIE_PATH = "/";

        private Dictionary<string, string> _outputOptions = new Dictionary<string, string>
        {
            { "pixel", "./pixel.gif" },
            { "gif", "./badge.gif" },
            { "flat", "./badge-flat.svg" },
            { "flat-gif", "./badge-flat.gif" },
            { "badge", "./badge.svg" }
        };

        private readonly IHttpContextAccessor _httpContextAccessor;
        private TelemetryClient _telemetry = new TelemetryClient();

        public BeaconController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        // GET api/UA-00000-0
        [HttpGet("{trackingId:regex(^UA-\\d+-\\d+)}/{*urlPath}")]
        public async Task<FileResult> Get(string trackingId, string urlPath)
        {
            var canContinue = true;

            if (string.IsNullOrEmpty(trackingId))
            {
                _telemetry.TrackTrace("No tracking ID was found in the request URL.", SeverityLevel.Error);
                canContinue = false;
            }

            var ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

            if (string.IsNullOrEmpty(ipAddress))
            {
                _telemetry.TrackTrace("Couldn't get the remote IP address.", SeverityLevel.Error);
                canContinue = false;
            }

            string clientId = Request.Cookies.FirstOrDefault(cookie => cookie.Key.Equals(CLIENT_ID, StringComparison.OrdinalIgnoreCase)).Value;

            if (string.IsNullOrEmpty(clientId))
            {
                clientId = GenerateUuid();
                _telemetry.TrackTrace($"No '{CLIENT_ID}' cookie was found in the request. A new value was computed: {clientId}.", SeverityLevel.Information);
            }

            var useReferer = IsQueryStringParamPresent(USE_REFERER);

            var referer = Request.Headers["Referer"];
            string pathToLog = null;

            if (useReferer)
            {
                if (string.IsNullOrEmpty(referer))
                {
                    _telemetry.TrackTrace("The 'useReferer' query string parameter was used in the request but no referrer HTTP header was found.", SeverityLevel.Error);
                    canContinue = false;
                }
                else
                {
                    pathToLog = referer;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(urlPath))
                {
                    _telemetry.TrackTrace("The 'useReferer' query string parameter was not used but the trailing URL path is missing.");
                    canContinue = false;
                }
                else
                {
                    pathToLog = urlPath;
                }
            }

            if (canContinue)
            {
                try
                {
                    await LogHit(trackingId, clientId, HitType.PageView, pathToLog, ipAddress, HttpContext.Request.Headers["User-Agent"].FirstOrDefault());
                }
                catch (Exception ex)
                {
                    _telemetry.TrackTrace(ex.Message, SeverityLevel.Error);
                }
            }

            Response.Cookies.Append(
                CLIENT_ID,
                clientId,
                new CookieOptions()
                {
                    Path = COOKIE_PATH
                });

            Response.Headers.Add("Expires", DateTime.UtcNow.ToString("r"));
            string imagePath = null;

            foreach (var option in _outputOptions)
            {
                if (IsQueryStringParamPresent(option.Key))
                {
                    imagePath = option.Value;

                    break;
                }

                imagePath = _outputOptions["badge"];
            }

            return File(imagePath, imagePath.EndsWith(".gif") ? "image/gif" : "image/svg+xml");
        }

        private async Task LogHit(string trackingId, string clientId, HitType hitType, string pagePath, string ipAddress, string userAgent)
        {
            if (string.IsNullOrEmpty(trackingId))
            {
                throw new ArgumentNullException(nameof(trackingId));
            }

            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            if (string.IsNullOrEmpty(pagePath))
            {
                throw new ArgumentNullException(nameof(pagePath));
            }

            if (string.IsNullOrEmpty(ipAddress))
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            var httpClient = HttpClientFactory.Create();
            var request = new HttpRequestMessage(HttpMethod.Post, GA_URI);
            request.Headers.Add("User-Agent", userAgent);

            var payload = new Dictionary<string, string>
            {
                { "v", "1" },
                { TRACKING_ID, trackingId },
                { CLIENT_ID, clientId },
                { HIT_TYPE, hitType.ToString().ToLower() },
                { PAGE_PATH, pagePath },
                { IP_ADDRESS, ipAddress }
            };

            request.Content = new FormUrlEncodedContent(payload);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _telemetry.TrackTrace($"The Google Measurement Protocol API did not return 2xx. Tracking ID: {trackingId}, client ID: {clientId}, IP address: {ipAddress}, page path: {pagePath}.", SeverityLevel.Error);
            }
            else
            {
                _telemetry.TrackTrace($"GA hit was logged. Tracking ID: {trackingId}, client ID: {clientId}, IP address: {ipAddress}, page path: {pagePath}.", SeverityLevel.Information);
            }
        }

        private string GenerateUuid()
        {
            var randomBytes = new byte[10];
            RandomNumberGenerator.Fill(randomBytes);
            var random = BitConverter.ToUInt32(randomBytes);
            var unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            return $"GA.1-2.{random}.{unixTimestamp}";
        }

        private long LongRandom(long min, long max, Random rand)
        {
            long result = rand.Next((int)(min >> 32), (int)(max >> 32));
            result = (result << 32);
            result = result | (long)rand.Next((int)min, (int)max);
            return result;
        }

        private bool IsQueryStringParamPresent(string queryStringParam) => Request
            .Query
            .Where(query => query.Key.Equals(queryStringParam, StringComparison.OrdinalIgnoreCase))
            .Any();
    }
}
