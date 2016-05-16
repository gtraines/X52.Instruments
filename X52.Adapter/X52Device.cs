using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X52.Adapter
{
    public class X52Device
    {
        public X52Device()
        {
            Proxy = new X52Proxy();
                
        }

        public X52Proxy Proxy { get; set; }
    }
}
