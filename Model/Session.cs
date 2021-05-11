using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoWinSlotNotifier
{
    public class Session
    {
        public string session_id;

        public string date;

        public int available_capacity;

        public int min_age_limit;

        public string vaccine;

        public IList<string> slots;
    }
}
