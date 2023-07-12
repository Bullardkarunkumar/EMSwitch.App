using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ets.Device.Parameters
{
    public static class ParametersTypeInfo
    {
        //TEMSWITCH parametrs
        public static readonly int MAX_EMCENTERS = 10;
        public static readonly int MAX_SLOTS_PER_EMCENTER = 7;
        public static readonly int MAX_EMCENTER_SLOTS = 70;
        public static readonly string DEFAULT_CONFIG_STRING = "0000000|0000000|0000000|0000000|0000000|0000000|0000000|0000000|0000000|0000000|";

        //TEMSwitch Errors
        public static readonly string ERROR_205 = "Interlock Error";
        public static readonly string ERROR_201 = "Error while trying to switch to state NC";
        public static readonly string ERROR_202 = "Error while trying to switch to state NO";
        public static readonly string ERROR_206 = "Error Switch A";
        public static readonly string ERROR_207 = "Error Switch B";
        public static readonly string ERROR_208 = "Switch Error";
        public static readonly string ERROR_211 = "Error Status Unknown";




    }
}
