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

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;
            this.config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Put the district number and date
            Dictionary<int, Center> availableCenters = new Dictionary<int, Center>();

            int districtId;
            while (true)
            {
                Console.WriteLine("Please enter the district: ");
                string districtName = Console.ReadLine();
                
                if (Constants.DistrictMapping.TryGetValue(districtName, out districtId))
                {
                    break;
                }
                Console.WriteLine("District not found, please refer valid values of district");
            }         

            int age_limit = Convert.ToInt32(config["Age_Limit"]);
            string userAgent = config["User_Agent"];
            string apiAddress = config["Api_Url"];
            string FeeType = config["Fee_Type"];

            DateTime startTime = DateTime.Now;

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", userAgent);

                string dateString = DateTime.Now.ToString("dd-MM-yyyy");
                string finalURL = string.Format(apiAddress, districtId, dateString);

                HttpResponseMessage response = await client.GetAsync(finalURL);
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
                    .AddText("Please open the console window to find out where you can get vaccinated!")
                    .Show();

                    availableCentersNewCall.ToList().ForEach(center =>
                    {
                        availableCenters.Add(center.center_id, center);
                    });
                }
                else
                {
                    _logger.LogInformation($"{DateTime.Now.ToString()}: No new open slots found");
                }

                if (availableCenters.Any())
                {
                    _logger.LogInformation("Open slots available:");
                    availableCenters.Values.ToList().ForEach(center =>
                    {
                        string LogLine = $"Pincode: {center.pincode}, Center: {center.name}, Vaccine: {center.sessions[0].vaccine}";
                        if (age_limit == 0)
                        {
                            HashSet<string> ages = new HashSet<string>();
                            center.sessions.ToList().ForEach(s => ages.Add(s.min_age_limit.ToString()));
                            LogLine += $" Ages: {String.Join(", ", ages.ToList())}";
                        }
                        
                        Console.WriteLine(LogLine);
                    });
                }

                Console.WriteLine("-------------------------------------------------------------------------------------------------");

                if (DateTime.Now > startTime.AddHours(6))
                {
                    startTime = DateTime.Now;
                    availableCenters.Clear();
                }

                await Task.Delay(60000, stoppingToken);
            }
        }
    }
}
