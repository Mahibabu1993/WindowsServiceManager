using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServiceManager
{
    class ServiceModel
    {
        public string Caption { get; set; }
        public string DisplayName { get; set; }
        public string Name { get; set; }
        public UInt32 ProcessId { get; set; }
        public Boolean Started { get; set; }
        public string StartMode { get; set; }
        public string State { get; set; }
        public string Status { get; set; }
    }
}
