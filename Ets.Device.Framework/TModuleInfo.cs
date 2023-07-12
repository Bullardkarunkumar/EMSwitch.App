using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;
using static Ets.Device.Framework.EptDeviceEnum;

namespace Ets.Device.Framework
{
    public class TModuleInfo
    {
        public TEMSModuleType ModuleType { get; set; }  // The module type
        public int RelayCount { get; set; }             // How many Relays in each module
        public int StateCount { get; set; }             // How many states each relay has
        public int FirstState { get; set; }             // Indicates whether the relay state is 0 or 1 based
        public int EMCSlotNum { get; set; }             // Slot number in the EMCenter   1 to 7
        public int EMCenterNum { get; set; }           // EMCenter Number or Address    0 to 9 (0 is the master)
        public int FirstSwitchNum { get; set; }        // Firt Switch number in the module


    }
    
}
