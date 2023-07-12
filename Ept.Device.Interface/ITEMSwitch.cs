using NPoco;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Ept.Device.Interface
{
    public interface ITEMSwitch //: TSwitchDriver
    {  
        void VerifyIDN();
        //TEMSwitch(const TParameter* pPI);
        void DoOnFirstInit();
        void DoOnSubsequentInit();
        void Finalize();
        void SetState(int switchNum, int iState);
        int GetState(int switchNum);
        void SetStateVector(char[] states);
        //void SetStateVector(int iState) { TSwitchDriver::SetStateVector(iState); };        

        string GetStateVector();
        void SetState(int moduleIndex, int switchNum, int iState);
        void SetState(int emcenterNum, int emcenterSlot, int switchNum, int switchType, int state);
        int GetState(int moduleIndex, int switchNum);
        int GetState(int emcenterNum, int emcenterSlot, int switchNum, int switchType);
        int ReturnPortCount();
        //const TModuleInfo* GetModuleList() { return Modules; }
        int GetStateFromResult(string result, int switchType);
        string GetStateStrFromState(int iState, int SwitchType);
        string ErrorMessage(string error);
        int CheckInterlock();
        int IsInterlockEquipment() { return 1; }
    }
}
