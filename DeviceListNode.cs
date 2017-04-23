using System;
using System.ComponentModel.Composition;

using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

using PS3Eye;

namespace VVVV.Nodes
{
    namespace VVVV.PS3Eye
    {
        [PluginInfo(Name = "DeviceList", Category = "PS3Eye", Author = "mino")]
        public class DeviceListNode : IPluginEvaluate, IPartImportsSatisfiedNotification
        {
            [Input("Update", IsBang = true, IsSingle = true)]
            ISpread<bool> FInUpdate;

            [Output("Camera Count")]
            ISpread<int> FOutCameraCount;

            [Output("ID")]
            ISpread<int> FOutID;

            [Output("UUID")]
            ISpread<string> FOutUUID;

            [Import()]
            public ILogger FLogger;

            public void OnImportsSatisfied()
            {
                Reset();
            }

            public void Evaluate(int SpreadMax)
            {

            }

            protected void Reset()
            {
                int count = CLEyeCamera.CameraCount;

                FOutCameraCount[0] = count;

                FOutID.SliceCount = count;
                FOutUUID.SliceCount = count;
                
                if(count > 0)
                {
                    for(int i=0; i<count; i++)
                    {
                        FOutID[i] = i;
                        FOutUUID[i] = CLEyeCamera.CameraUUID(i).ToString();
                    }
                }
            }
        }
    }
}