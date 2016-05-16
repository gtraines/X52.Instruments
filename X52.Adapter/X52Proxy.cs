using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace X52.Adapter
{
    public class X52Proxy
    {
        public X52Proxy()
        {
            AttachedDevices = new List<IntPtr>();
            Output = new DirectOutput();
            Output.Initialize("TestName");

            Output.Enumerate(((device, target) =>
            {
                Console.WriteLine(target);
            }));

            foreach (var entry in AttachedDevices)
            {
                Console.WriteLine(entry.ToInt32());
            }
        }

        private DirectOutput.EnumerateCallback EnumerateDevices()
        {
            return (device, target) =>
            {
                AttachedDevices.Add(device);
            };
        }

        public DirectOutput Output { get; set; }
        public List<IntPtr> AttachedDevices { get; set; }
    }
}