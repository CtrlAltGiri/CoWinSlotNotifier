using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoWinSlotNotifier
{
    public class Center
    {
        public int center_id;

        public string name;

        public string address;

        public string state_name;

        public string district_name;

        public string block_name;

        public long pincode;

        public int lat;

        [JsonProperty("long")]
        public int longValue;

        [JsonProperty("from")]
        public string fromValue;

        public string to;

        public string fee_type;

        public IList<Session> sessions;
    }
}
