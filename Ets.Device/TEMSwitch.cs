using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading;
using Ept.Device.Interface;
using Ets.Device.Framework;
using Ets.Device.Parameters;
using NPoco;
using static Ets.Device.Framework.EptDeviceEnum;

namespace Ets.Device
{
    public class TEMSwitch : ITEMSwitch //TSwitchDriver
    {
        //TModuleInfo[] ModuleTypeInfo; // Defines properties of each module
        readonly TModuleInfo[] Modules = new TModuleInfo[ParametersTypeInfo.MAX_EMCENTER_SLOTS];  // Holds list of installed modules
                                                                                                  //vector<int> ModuleInfoIndex;  // Holds the module index for each switch in the array
        readonly Dictionary<int, int> StateShadow = new Dictionary<int, int>();                  //map<int, int> StateShadow;     // Saves the previous state (to reduce gpib writes)
        int SwitchedTime = DateTime.Now.Microsecond;

        bool useShadow;
        readonly int SwitchCount;  // Number of switches present
        readonly string EX_EQUIP_NAME = "";//Need to define some where
        readonly int[] ModuleInfoIndex;
        readonly bool m_bIsVISA = false; //Need to define
        bool bVISAAsyncLocalOverride = false;//Need to define

        readonly TModuleInfo[] ModuleTypeInfo = {
                                        new TModuleInfo { ModuleType = TEMSModuleType.EMSType_Unused, StateCount = 0, FirstState = 0, EMCSlotNum = 0, EMCenterNum=0, FirstSwitchNum = 0,},
                                        new TModuleInfo { ModuleType = TEMSModuleType.EMSType_2XSP2T, StateCount = 2, FirstState = 2, EMCSlotNum = 0, EMCenterNum=0, FirstSwitchNum = 0},
                                        new TModuleInfo { ModuleType = TEMSModuleType.EMSType_4XSP2T, StateCount = 4, FirstState = 2, EMCSlotNum = 0, EMCenterNum=0, FirstSwitchNum = 0},
                                        new TModuleInfo { ModuleType = TEMSModuleType.EMSType_2XSP6T, StateCount = 2, FirstState = 6, EMCSlotNum = 0, EMCenterNum=0, FirstSwitchNum = 0},
                                        new TModuleInfo { ModuleType = TEMSModuleType.EMSType_EMControl, StateCount = 4, FirstState = 2, EMCSlotNum =0, EMCenterNum=0, FirstSwitchNum = 0},
                                        new TModuleInfo { ModuleType = TEMSModuleType.EMSType_1XSP6T, StateCount = 1, FirstState = 6, EMCSlotNum = 0, EMCenterNum=0, FirstSwitchNum = 0},
                                        new TModuleInfo { ModuleType = TEMSModuleType.EMSType_1XSP2T, StateCount = 1, FirstState = 2, EMCSlotNum = 0, EMCenterNum=0, FirstSwitchNum = 0}
                                        };

        public TEMSwitch(TParameter pPI) //: base(pPI, 0, 10000, 0) // implement TParameter
        {
            if (m_bIsVISA)
            {
                string srcName = ""; //"TCPIP::" + pPmtr.GetASParameter(szIPAddress) + "::INSTR";
                                     //VISASet(srcName);
                                     // VISASetEOS(0xa);
            }
            string config = ""; //pPmtr.GetASParameter(szUserInitConfig, DEFAULT_CONFIG_STRING);
            int slotNum = 1;
            int emcenterNum = 0;
            int moduleIndex = 0;
            int totalSwitchCount = 0;

            int i = 1;
            while (i < ParametersTypeInfo.MAX_EMCENTER_SLOTS + ParametersTypeInfo.MAX_EMCENTERS)
            {
                if (config[i] == '|')
                {
                    i++;
                    slotNum = 1;
                    emcenterNum++;
                    continue;
                }
                Modules[moduleIndex] = ModuleTypeInfo[config[i] - '0'];
                Modules[moduleIndex].EMCenterNum = emcenterNum;
                Modules[moduleIndex].EMCSlotNum = slotNum;
                Modules[moduleIndex].FirstSwitchNum = totalSwitchCount;

                for (int x = 0; x < Modules[moduleIndex].RelayCount; x++)
                {
                    if (ModuleInfoIndex != null)
                    {
                        ModuleInfoIndex.ToList().Add(moduleIndex);
                    }
                }
                totalSwitchCount += Modules[moduleIndex].RelayCount;
                moduleIndex++;
                slotNum++;
                i++;
            }
            SwitchCount = totalSwitchCount;
            StateShadow.Clear();
            useShadow = false;
        }

        public TModuleInfo[] GetModuleList() { return Modules; }
    public void DoOnFirstInit()
        {
            try
            {
                // Verify the presence of the correct EMSwitch cards in the EMCenters, i.e., verify the configuration
                try
                {
                    VerifyIDN();
                }
                catch (Exception)
                {
                    // This is just a VISA catch to see if running in synchronous mode fixes the problem
                    if (!m_bIsVISA)
                        throw new Exception("DoOnFirstInit");
                    bVISAAsyncLocalOverride = false;
                }

                if (!bVISAAsyncLocalOverride)
                    VerifyIDN();

                DoOnSubsequentInit();
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("DoOnFirstInit");
            }
        }
        public void VerifyIDN()
        {
            try
            {
                for (int i = 0; i < ParametersTypeInfo.MAX_EMCENTER_SLOTS; i++)
                {
                    if (Modules[i].ModuleType == TEMSModuleType.EMSType_Unused)
                        continue;

                    int address = Modules[i].EMCenterNum * 10 + Modules[i].EMCSlotNum;
                    string idn = string.Format("{0}:*IDN?", address);

                    bool error = false;
                    switch (Modules[i].ModuleType)
                    {
                        case TEMSModuleType.EMSType_2XSP2T:
                            if ((idn.IndexOf("EMSwitch 7001-001") < 0) && (idn.IndexOf("EMSwitch 7001-011") < 0) &&
                                (idn.IndexOf("EMSwitch 7001-023") < 0) && (idn.IndexOf("EMSwitch 7001-026") < 0))
                                error = true;
                            break;
                        case TEMSModuleType.EMSType_4XSP2T:
                            if ((idn.IndexOf("EMSwitch 7001-002") < 0) && (idn.IndexOf("EMSwitch 7001-024") < 0) &&
                                (idn.IndexOf("EMSwitch 7001-012") < 0) && (idn.IndexOf("EMSwitch 7001-027") < 0))
                                error = true;
                            break;
                        case TEMSModuleType.EMSType_2XSP6T:
                            if ((idn.IndexOf("EMSwitch 7001-003") < 0) && (idn.IndexOf("EMSwitch 7001-013") < 0))
                                error = true;
                            break;
                        case TEMSModuleType.EMSType_EMControl:
                            if ((idn.IndexOf("ETS-Lindgren, EMControl") < 0) && (idn.IndexOf("D.A.R.E!!, RadiControl") < 0))
                                error = true;
                            break;
                        case TEMSModuleType.EMSType_1XSP6T:
                            if ((idn.IndexOf("EMSwitch 7001-005") < 0) && (idn.IndexOf("EMSwitch 7001-025") < 0))
                                error = true;
                            break;
                        case TEMSModuleType.EMSType_1XSP2T:
                            if (idn.IndexOf("EMSwitch 7001-021") < 0)
                                error = true;
                            break;
                    }
                    if (error)
                    {
                        throw new Exception(@"A valid EMSwitch was not found in EMCenter: " + Modules[i].EMCenterNum +
                            " Slot: " + Modules[i].EMCSlotNum + EX_EQUIP_NAME +
                            "  Received \"" + idn + "\" for an ID string.");
                    }
                }
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("VerifyIDN");
            }
        }

        public void DoOnSubsequentInit()
        {
            try
            {
                useShadow = false;
                StateShadow.Clear();
                //TSwitchDriver.DoOnSubsequentInit();
                // if (!bBlankStates)
                //  useShadow = true;
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("DoOnSubsequentInit");
            }
        }

        public void Finalize()
        {
            try
            {
                //TSwitchDriver.Finalize();
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("Finalize");
            }
        }

        public void SetState(int switchNum, int iState)
        {
            try
            {
                if (switchNum >= ModuleInfoIndex.Count())
                    throw new Exception("Switch number is greater than configured number of switches");

                int moduleIndex = ModuleInfoIndex[switchNum]; // find out which module this is in
                int switchOffset = switchNum - Modules[moduleIndex].FirstSwitchNum; // and which switch in that module

                SetState(moduleIndex, switchOffset, iState);
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("SetState");
            }
        }

        public void SetState(int moduleIndex, int switchNum, int iState)
        {
            try
            {
                int type = (int)Modules[moduleIndex].ModuleType;
                if ((iState > 1) && ((type == (int)TEMSModuleType.EMSType_1XSP2T) || (type == (int)TEMSModuleType.EMSType_2XSP2T) || (type == (int)TEMSModuleType.EMSType_4XSP2T) || ((type == (int)TEMSModuleType.EMSType_EMControl) && (switchNum < 2))))
                    return;
                if ((iState > 2) && ((type == (int)TEMSModuleType.EMSType_EMControl) && (switchNum >= 2)))
                    return;
                if ((iState > 5) && (type == (int)TEMSModuleType.EMSType_2XSP6T || type == (int)TEMSModuleType.EMSType_1XSP6T))
                    return;

                SetState(Modules[moduleIndex].EMCenterNum, Modules[moduleIndex].EMCSlotNum, switchNum, type, iState);
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("SetState");
            }
        }

        public void SetState(int emcenterNum, int emcenterSlot, int switchNum, int switchType, int state)
        {
            string result = "";
            try
            {
                string cmd;
                int address = emcenterNum * 10 + emcenterSlot;
                int globalSwitchNum = address * 10 + switchNum;


                if (useShadow)
                {
                    int prevState;
                    if (StateShadow.TryGetValue(globalSwitchNum, out prevState))
                    {
                        if (prevState == state) // state same as before
                            return;
                    }
                    StateShadow[globalSwitchNum] = state;
                }

                for (int retry = 0; retry < 5; retry++)
                {
                    //string result;
                    if (switchType == (int)TEMSModuleType.EMSType_EMControl)
                    {
                        if ((switchNum == 0) || (switchNum == 1)) // AUX port
                        {
                            string stateStr = GetStateStrFromState(state, switchType);
                            result = string.Format("{0}AUX{1} {2}", address, switchNum + 1, stateStr); // set state
                            result = string.Format("{0}AUX{1}?", address, switchNum + 1); // query the port setting and convert to an integer
                        }
                        else if (switchNum == 2) // Pol switch on Device A
                        {
                            if (state == 2)
                                result = string.Format("{0}AP2", address); // set bypass
                            else if (state == 1)
                                result = string.Format("{0}APH", address); // set horizontal
                            else if (state == 0)
                                result = string.Format("{0}APV", address); // vertical
                            result = string.Format("{0}AP?", address); // query the port setting and convert to an integer
                        }
                        else if (switchNum == 3) // Pol switch on Device B
                        {
                            if (state == 2)
                                result = string.Format("{0}BP2", address); // set bypass
                            else if (state == 1)
                                result = string.Format("{0}BPH", address); // set horizontal
                            else if (state == 0)
                                result = string.Format("{0}BPV", address); // set vertical
                            result = string.Format("{0}BP?", address); // query the port setting and convert to an integer
                        }
                    }
                    else
                    {
                        char switchChar = (char)(switchNum + 'A'); // convert 0 to A, 1 to B, etc.
                        string stateStr = GetStateStrFromState(state, switchType);
                        result = string.Format("{0}:INT_RELAY_{1}_{2}", address, switchChar, stateStr); // set the state
                        result = string.Format("{0}:INT_RELAY_{1}?", address, switchChar); // query the port setting and convert to an integer
                    }

                    if (result.StartsWith("ERROR"))
                        throw new Exception(ErrorMessage(result));

                    Thread.Sleep(50);

                    int currentState = GetStateFromResult(result, switchType);
                    if (currentState == state)
                        return;
                }

                throw new Exception("Unable to set switch to target state");
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("SetState");
            }
        }

        public int GetState(int switchNum)
        {
            try
            {
                if (switchNum >= ModuleInfoIndex.Count())
                    throw new Exception("Switch number is greater than configured number of switches");

                int moduleIndex = ModuleInfoIndex[switchNum]; // find out which module this is in
                int switchOffset = switchNum - Modules[moduleIndex].FirstSwitchNum; // and which switch in that module

                return GetState(moduleIndex, switchOffset);
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("GetState");
            }
        }

        public int GetState(int moduleIndex, int switchNum)
        {
            try
            {
                return GetState(Modules[moduleIndex].EMCenterNum, Modules[moduleIndex].EMCSlotNum, switchNum, (int)Modules[moduleIndex].ModuleType);
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("GetState");
            }
        }

        public int GetState(int emcenterNum, int emcenterSlot, int switchNum, int switchType)
        {
            try
            {
                string result = "";
                int address = emcenterNum * 10 + emcenterSlot;

                if (switchType == (int)TEMSModuleType.EMSType_EMControl)
                {
                    if ((switchNum == 0) || (switchNum == 1)) // AUX port
                    {
                        result = string.Format("{0}AUX{1}?", address, switchNum + 1); // query the port setting and convert to an integer
                    }
                    else if (switchNum == 2) // Pol switch on Device A
                    {
                        result = string.Format("{0}AP?", address); // query the port setting and convert to an integer
                    }
                    else if (switchNum == 3) // Pol switch on Device B
                    {
                        result = string.Format("{0}BP?", address); // query the port setting and convert to an integer
                    }
                }
                else
                {
                    char switchChar = (char)(switchNum + 'A'); // convert 0 to A, 1 to B, etc.
                    result = string.Format("{0}:INT_RELAY_{1}?", address, switchChar); // query the port setting and convert to an integer
                }

                int currentState = GetStateFromResult(result, switchType);

                int globalSwitchNum = address * 10 + switchNum;
                StateShadow[globalSwitchNum] = currentState;

                return currentState;
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("GetState");
            }
        }

        public int ReturnPortCount()
        {
            return SwitchCount;
        }

        public int GetStateFromResult(string result, int switchType)
        {
            int res = 0;
            try
            {
                if (string.IsNullOrEmpty(result))
                    return 9;
                if (result.StartsWith("ERROR_205")) // Interlock error
                {
                    res = 0; // state is NC
                    return res;
                }
                else if (result.StartsWith("ERROR"))
                {
                    res = 9;
                    return res;
                }

                result = result.Trim(); // remove the \n or \r\n at the end

                if (switchType == (int)TEMSModuleType.EMSType_EMControl)
                {
                    res = int.Parse(result);
                }
                else if (switchType == (int)TEMSModuleType.EMSType_2XSP6T || switchType == (int)TEMSModuleType.EMSType_1XSP6T)
                {
                    res = int.Parse(result) - 1;
                }
                else
                {
                    if (result == "NC")
                        res = 0;
                    else if (result == "NO")
                        res = 1;
                    else
                        res = 9; // not a valid state
                }
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("GetStateFromResult");
            }
            return res;
        }
        public void SetStateVector(string states)
        {
            try
            {
                int x = 0;
                int n = ReturnPortCount();
                foreach (char c in states)
                {
                    if (x >= n)
                        break;
                    if ((c != '*') && (c != ',')) // skip delimiters and placeholders
                    {
                        SetState(x, c - '0');
                        x++;
                    }
                }
                SwitchedTime = Environment.TickCount; // for cascaded delays
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("SetStateVector");
            }
        }

        public string GetStateVector()
        {
            try
            {
                int n = ReturnPortCount();
                int count = 0;
                StringBuilder stateVector = new StringBuilder();
                for (int i = 0; i < ParametersTypeInfo.MAX_EMCENTER_SLOTS; i++)
                {
                    if (count >= n)
                        break;
                    if (Modules[i].ModuleType == TEMSModuleType.EMSType_Unused)
                    {
                        stateVector.Append("*,"); // place holder for empty slot
                        continue;
                    }
                    for (int x = 0; x < Modules[i].RelayCount; x++)
                    {
                        count++;
                        int state = GetState(i, x);
                        stateVector.Append(state);
                    }
                    stateVector.Append(",");
                }
                return stateVector.ToString();
            }
            catch (Exception)
            {
                // Handle or rethrow the exception as needed
                throw new Exception("GetStateVector");
            }
        }

        public string ErrorMessage(string error)
        {
            if (error.Contains("ERROR_205"))
                return ParametersTypeInfo.ERROR_205;
            else if (error.Contains("ERROR_201"))
                return ParametersTypeInfo.ERROR_201;
            else if (error.Contains("ERROR_202"))
                return ParametersTypeInfo.ERROR_202;
            else if (error.Contains("ERROR_206"))
                return ParametersTypeInfo.ERROR_206;
            else if (error.Contains("ERROR_207"))
                return ParametersTypeInfo.ERROR_207;
            else if (error.Contains("ERROR_208"))
                return ParametersTypeInfo.ERROR_208;
            else if (error.Contains("ERROR_211"))
                return ParametersTypeInfo.ERROR_211;
            else
                return error;
        }

        //-----------------------------------------------------------------------------
        //Returns the string equivalent of the state to be sent to the device
        public string GetStateStrFromState(int iState, int switchType)
        {
            string result = "";
            try
            {
                if (switchType == (int)TEMSModuleType.EMSType_EMControl)
                {
                    switch (iState)
                    {
                        case 0:
                            result = "OFF";
                            break;
                        case 1:
                            result = "ON";
                            break;
                    }
                }
                else if (switchType == (int)TEMSModuleType.EMSType_2XSP6T || switchType == (int)TEMSModuleType.EMSType_1XSP6T)
                {
                    result = (iState + 1).ToString();
                }
                else
                {
                    switch (iState)
                    {
                        case 0:
                            result = "NC";
                            break;
                        case 1:
                            result = "NO";
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // Handle exception here
            }
            return result;
        }

        public void SetStateVector(char[] states)
        {
            try
            {
                int x = 0;
                int n = ReturnPortCount();
                while (x < n)
                {
                    if (states[x] == '\0')
                        break;
                    if ((states[x] != '*') && (states[x] != ','))
                    {
                        SetState(x, states[x] - 0x30);
                        x++;
                    }
                    x++;
                }
                SwitchedTime = DateTime.Now.Microsecond; // for cascaded delays
            }
            catch (Exception ex)
            {
                // Handle exception
            }
        }

        public int CheckInterlock()
        {
            for (int i = 0; i < ParametersTypeInfo.MAX_EMCENTER_SLOTS; i++)
            {
                if (Modules[i].ModuleType == TEMSModuleType.EMSType_Unused || Modules[i].ModuleType == TEMSModuleType.EMSType_EMControl)
                    continue;
                int address = Modules[i].EMCenterNum * 10 + Modules[i].EMCSlotNum;
                string result = string.Format("{0}:INT_RELAY_A?", address);
                if (result.Contains("ERROR_205"))
                    return 1;
            }
            return 0;
        }

        int IsInterlockEquipment() { return 1; }

        public class TParameter
        {
        }
    }
}
