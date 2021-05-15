using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Extensions.Configuration;

namespace CoWinSlotNotifier
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private IConfiguration config;

        private int age_limit;

        private string userAgent;

        private string apiAddress;

        private string FeeType;

        private string districtName;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;
            this.config = config;

            age_limit = Convert.ToInt32(config["Age_Limit"]);
            userAgent = config["User_Agent"];
            apiAddress = config["Api_Url"];
            FeeType = config["Fee_Type"];
            districtName = config["District"];

            _logger.LogInformation($"Finding slots for {districtName}...");

            int districtId;
            if (!Constants.DistrictMapping.TryGetValue(districtName, out districtId))
            {
                _logger.LogError("District not found, please refer valid values of district from CSV file");
                return;
            }

            string dateString = DateTime.Now.ToString("dd-MM-yyyy");
            apiAddress = string.Format(apiAddress, districtId, dateString);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Put the district number and date
            Dictionary<int, Center> availableCenters = new Dictionary<int, Center>();

            DateTime startTime = DateTime.Now;
            _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);
            int times = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", userAgent);

                HttpResponseMessage response = await client.GetAsync(this.apiAddress);
                string responseBody = await response.Content.ReadAsStringAsync();

                var res = JsonConvert.DeserializeObject(responseBody);
                List<Center> centers = JsonConvert.DeserializeObject<List<Center>>((res as Newtonsoft.Json.Linq.JObject)["centers"].ToString());

                var availableCentersNewCall = centers.Where(center =>
                {
                    if (FeeType != center.fee_type)
                    {
                        return false;
                    }

                    return center.sessions?.ToList().Where(session =>
                    {
                        if (session.available_capacity > 0)
                        {
                            if (session.min_age_limit == age_limit || age_limit == 0)
                            {
                                return true;
                            }
                        }
                        return false;

                    }).ToList().Any() == true;
                });

                availableCentersNewCall = availableCentersNewCall.Where(center =>
                {
                    return !availableCenters.ContainsKey(center.center_id);
                });

                if (availableCentersNewCall.Any())
                {
                    new ToastContentBuilder()
                    .AddText("There are open slot(s)!")
                    .AddText("Open the console window to see where you can be vaccinated?")
                    .Show();

                    availableCentersNewCall.ToList().ForEach(center =>
                    {
                        availableCenters.Add(center.center_id, center);
                    });
                }
                else
                {
                    if (times == 100) {
                        times = 0;
                        _logger.LogInformation($"{DateTime.Now.ToString()}: No new open slots found");
                    }
                }

                // Remove centers that don't have vaccines anymore
                //availableCenters.Keys.ToList().ForEach(key => {
                //    if (!availableCentersNewCall.Any(c => c.center_id == key)){
                //        availableCenters.Remove(key);
                //    }
                //});

                if (availableCenters.Any())
                {
                    _logger.LogInformation("Open slots available:");
                    availableCenters.Values.ToList().ForEach(center =>
                    {
                        string LogLine = $"Pincode: {center.pincode} | Center: {center.name} | Vaccine: {center.sessions[0].vaccine} ";

                        HashSet<string> ages = new HashSet<string>();
                        HashSet<string> dates = new HashSet<string>();
                        center.sessions.ToList().ForEach(s => {
                            if (s.available_capacity > 0)
                            {
                                ages.Add(s.min_age_limit.ToString());
                                dates.Add(s.date.ToString() + " - " + s.available_capacity);
                            }
                        });

                        LogLine += $"| Ages: {String.Join(", ", ages.ToList())} ";
                        LogLine += $"| Dates: {String.Join(", ", dates.ToList())} ";

                        Console.WriteLine(LogLine);
                    });
                }

                if (DateTime.Now > startTime.AddMinutes(1))
                {
                    startTime = DateTime.Now;
                    availableCenters.Clear();
                }

                Console.WriteLine("-------------------------------------------------------------------------------------------------");

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
