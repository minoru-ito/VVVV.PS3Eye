using System;
using System.ComponentModel.Composition;
//using System.Runtime.InteropServices;

using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

//using FeralTic.Resources;
using FeralTic.DX11;
using FeralTic.DX11.Resources;

//using SlimDX;
//using SlimDX.Direct3D11;
using SlimDX.DXGI;

using PS3Eye;

namespace VVVV.DX11.Nodes
{
    namespace VVVV.PS3Eye
    {
        [PluginInfo(Name = "PS3Eye", Category = "PS3Eye", Version = "DX11", Author = "mino")]
        public class PS3EyeNode : IPluginEvaluate, IPartImportsSatisfiedNotification, IDX11ResourceHost, IDisposable
        {
            #region input & output pins
            [Input("ID", MinValue = 0, MaxValue = CLEyeCamera.CAMERA_MAX - 1)]
            IDiffSpread<int> FInID;

            [Input("FrameRate", MinValue = 15, MaxValue = 125, DefaultValue = 30)]
            IDiffSpread<int> FInFrameRate;

            [Input("ColorMode", DefaultEnumEntry = "CLEYE_COLOR_PROCESSED")]
            IDiffSpread<CLEyeCameraColorMode> FInColorMode;

            [Input("Resolution", DefaultEnumEntry = "CLEYE_VGA")]
            IDiffSpread<CLEyeCameraResolution> FInResolution;

            [Input("AutoGain", DefaultBoolean = true)]
            IDiffSpread<bool> FInAutoGain;

            [Input("Gain", MinValue = 0, MaxValue = 79)]
            IDiffSpread<int> FInGain;

            [Input("AutoExposure", DefaultBoolean = true)]
            IDiffSpread<bool> FInAutoExposure;

            [Input("Exposure", MinValue = 0, MaxValue = 511)]
            IDiffSpread<int> FInExposure;

            [Input("AutoWhiteBalance", DefaultBoolean = true)]
            IDiffSpread<bool> FInAutoWhiteBalance;

            [Input("WhiteBalanceRed", MinValue = 0, MaxValue = 255)]
            IDiffSpread<int> FInWhiteBalanceRed;

            [Input("WhiteBalanceGreen", MinValue = 0, MaxValue = 255)]
            IDiffSpread<int> FInWhiteBalanceGreen;

            [Input("WhiteBalanceBlue", MinValue = 0, MaxValue = 255)]
            IDiffSpread<int> FInWhiteBalanceBlue;

            [Input("HorizontalFlip")]
            IDiffSpread<bool> FInHorizontalFlip;

            [Input("VerticalFlip")]
            IDiffSpread<bool> FInVerticalFlip;

            [Input("HorizontalKeystone", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInHorizontalKeystone;

            [Input("VerticalKeystone", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInVerticalKeystone;

            [Input("XOffset", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInXOffset;

            [Input("YOffset", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInYOffset;

            [Input("Rotation", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInRotation;

            [Input("Zoom", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInZoom;

            [Input("LensCorrection1", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInLensCorrection1;

            [Input("LensCorrection2", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInLensCorrection2;

            [Input("LensCorrection3", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInLensCorrection3;

            [Input("LensBrightness", MinValue = -500, MaxValue = 500)]
            IDiffSpread<int> FInLensBrightness;

            // LED on/off by Enabled pin
            //[Input("LED")]
            //IDiffSpread<bool> FInLED;

            [Input("Enabled")]
            IDiffSpread<bool> FInEnabled;

            [Output("Texture Out")]
            ISpread<DX11Resource<DX11DynamicTexture2D>> FOutTexture;
            #endregion

            [Import()]
            public ILogger FLogger;

            CLEyeCamera Camera;

            //bool created = false;
            bool disposed = false;
            bool invalidate = false;

            public void OnImportsSatisfied()
            {
                //Camera = new CLEyeCamera();
            }

            #region dispose
            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (!disposed)
                    {
                        disposed = true;

                        if(Camera != null)
                        {
                            CLEyeCamera.IsCreated[Camera.ID] = false;

                            Camera.Stop();
                            Camera.Dispose();
                            Camera = null;
                        }

                        if (FOutTexture.SliceCount > 0)
                        {
                            if (FOutTexture[0] != null)
                                FOutTexture[0].Dispose();
                        }
                    }
                }
            }
            #endregion

            protected void CreateCamera()
            {
                // no camera
                if (CLEyeCamera.CameraCount == 0)
                {
                    FLogger.Log(LogType.Error, "Could not find any PS3Eye cameras!");

                    return;
                }

                // check index is less than camera count
                if (FInID[0] < CLEyeCamera.CameraCount)
                {
                    // check camera is created or not
                    if (!CLEyeCamera.IsCreated[FInID[0]])
                    {
                        Camera = new CLEyeCamera();

                        // set framerate, colormode, resolution before call Camera.Create()
                        Camera.Framerate = FInFrameRate[0];
                        Camera.ColorMode = FInColorMode[0];
                        Camera.Resolution = FInResolution[0];

                        // create camera
                        if (Camera.Create(CLEyeCamera.CameraUUID(FInID[0])))
                        {
                            Camera.ID = FInID[0];

                            CLEyeCamera.IsCreated[Camera.ID] = true;

                            // set camera properties
                            SetCamera();
                        }
                        else
                        {
                            FLogger.Log(LogType.Error, "Could not create PS3Eye camera!");

                            return;
                        }
                    }
                    else
                    {
                        FLogger.Log(LogType.Error, "PS3Eye camera (index:" + FInID[0] + ") is already created!");

                        return;
                    }
                }
                else
                {
                    FLogger.Log(LogType.Error, "Could not find PS3Eye camera of ID " + FInID[0]);

                    return;
                }
            }

            // set camera properties
            protected void SetCamera()
            {
                if (Camera != null)
                {
                    Camera.AutoGain = FInAutoGain[0];
                    Camera.Gain = FInGain[0];
                    Camera.AutoExposure = FInAutoExposure[0];
                    Camera.Exposure = FInExposure[0];
                    Camera.AutoWhiteBalance = FInAutoWhiteBalance[0];
                    Camera.WhiteBalanceRed = FInWhiteBalanceRed[0];
                    Camera.WhiteBalanceGreen = FInWhiteBalanceGreen[0];
                    Camera.WhiteBalanceBlue = FInWhiteBalanceBlue[0];
                    Camera.HorizontalFlip = FInHorizontalFlip[0];
                    Camera.VerticalFlip = FInVerticalFlip[0];
                    Camera.HorizontalKeystone = FInHorizontalKeystone[0];
                    Camera.VerticalKeystone = FInVerticalKeystone[0];
                    Camera.XOffset = FInXOffset[0];
                    Camera.YOffset = FInYOffset[0];
                    Camera.Rotation = FInRotation[0];
                    Camera.Zoom = FInZoom[0];
                    Camera.LensCorrection1 = FInLensCorrection1[0];
                    Camera.LensCorrection2 = FInLensCorrection2[0];
                    Camera.LensCorrection3 = FInLensCorrection3[0];
                    Camera.LensBrightness = FInLensBrightness[0];
                }
            }

            public void Evaluate(int SpreadMax)
            {
                if (FOutTexture[0] == null)
                    FOutTexture[0] = new DX11Resource<DX11DynamicTexture2D>();

                if (Camera != null)// && Camera.Running)
                {
                    if (FInAutoGain.IsChanged)
                        Camera.AutoGain = FInAutoGain[0];

                    if (FInGain.IsChanged)
                        Camera.Gain = FInGain[0];

                    if (FInAutoExposure.IsChanged)
                        Camera.AutoExposure = FInAutoExposure[0];

                    if (FInExposure.IsChanged)
                        Camera.Exposure = FInExposure[0];

                    if (FInAutoWhiteBalance.IsChanged)
                        Camera.AutoWhiteBalance = FInAutoWhiteBalance[0];

                    if (FInWhiteBalanceRed.IsChanged)
                        Camera.WhiteBalanceRed = FInWhiteBalanceRed[0];

                    if (FInWhiteBalanceGreen.IsChanged)
                        Camera.WhiteBalanceGreen = FInWhiteBalanceGreen[0];

                    if (FInWhiteBalanceBlue.IsChanged)
                        Camera.WhiteBalanceBlue = FInWhiteBalanceBlue[0];

                    if (FInHorizontalFlip.IsChanged)
                        Camera.HorizontalFlip = FInHorizontalFlip[0];

                    if (FInVerticalFlip.IsChanged)
                        Camera.VerticalFlip = FInVerticalFlip[0];

                    if (FInHorizontalKeystone.IsChanged)
                        Camera.HorizontalKeystone = FInHorizontalKeystone[0];

                    if (FInVerticalKeystone.IsChanged)
                        Camera.VerticalKeystone = FInVerticalKeystone[0];

                    if (FInXOffset.IsChanged)
                        Camera.XOffset = FInXOffset[0];

                    if (FInYOffset.IsChanged)
                        Camera.YOffset = FInYOffset[0];

                    if (FInRotation.IsChanged)
                        Camera.Rotation = FInRotation[0];

                    if (FInZoom.IsChanged)
                        Camera.Zoom = FInZoom[0];

                    if (FInLensCorrection1.IsChanged)
                        Camera.LensCorrection1 = FInLensCorrection1[0];

                    if (FInLensCorrection2.IsChanged)
                        Camera.LensCorrection2 = FInLensCorrection2[0];

                    if (FInLensCorrection3.IsChanged)
                        Camera.LensCorrection3 = FInLensCorrection3[0];

                    if (FInLensBrightness.IsChanged)
                        Camera.LensBrightness = FInLensBrightness[0];

                    //if (FInLED.IsChanged)
                    //    Camera.LED = FInLED[0];

                    //if (Camera.GetFrame())
                    //{
                    //    invalidate = true;
                    //}
                    //else
                    //{
                    //    FLogger.Log(LogType.Debug, "get frame failed");
                    //    invalidate = false;
                    //}
                }

                // below changes must recreate camera object
                // this block must locate before "if(FInEnabled.IsChanged)"
                if (FInID.IsChanged || FInFrameRate.IsChanged || FInColorMode.IsChanged || FInResolution.IsChanged)
                {
                    // recreate texture
                    if (FOutTexture[0] != null)
                    {
                        FOutTexture[0].Dispose();
                        FOutTexture[0] = null;

                        FOutTexture[0] = new DX11Resource<DX11DynamicTexture2D>();
                    }

                    // dispose camera if not null
                    if (Camera != null)
                    {
                        CLEyeCamera.IsCreated[Camera.ID] = false;

                        Camera.Stop();
                        Camera.Dispose();
                        Camera = null;
                    }

                    // create & start camera if enabled (if not enabled, same operation will do at FInEnabled.IsChanged)
                    if (FInEnabled[0])
                    {
                        CreateCamera();

                        if (Camera != null)
                            Camera.Start();
                    }
                }

                if(FInEnabled.IsChanged)
                {
                    if(FInEnabled[0])
                    {
                        // not created camera yet
                        if (Camera == null)
                            CreateCamera();

                        // start camera
                        if (Camera != null && !Camera.Running)
                            Camera.Start();
                    }
                    else
                    {
                        if(Camera != null && Camera.Running)
                        {
                            Camera.Stop();
                        }
                    }
                }
            }

            unsafe public void Update(DX11RenderContext context)
            {
                if (Camera == null || !Camera.Running) return;

                int width, height;
                Format format;

                if (!FOutTexture[0].Contains(context))
                {
                    if (FInResolution[0] == CLEyeCameraResolution.CLEYE_VGA)
                    {
                        width = 640;
                        height = 480;
                    }
                    else
                    {
                        width = 320;
                        height = 240;
                    }

                    if (FInColorMode[0] == CLEyeCameraColorMode.CLEYE_COLOR_PROCESSED || FInColorMode[0] == CLEyeCameraColorMode.CLEYE_COLOR_RAW)
                        format = Format.B8G8R8A8_UNorm;
                    else
                        format = Format.R8_UNorm;

                    FOutTexture[0][context] = new DX11DynamicTexture2D(context, width, height, format);
                }

                //if(invalidate)
                if (Camera.GetFrame())
                {
                    //byte[] mdata = new byte[320 * 240];
                    //object _lock = new object();
                    //lock (_lock)
                    //    Marshal.Copy(Camera.Data, mdata, 0, 320 * 240);

                    //FOutTexture[0][context].WriteDataStride(mdata);

                    // texture broken with WriteData() when MONO && QVGA. use WriteDataPitch().
                    if (FInColorMode[0] == CLEyeCameraColorMode.CLEYE_MONO_PROCESSED || FInColorMode[0] == CLEyeCameraColorMode.CLEYE_MONO_RAW)
                        FOutTexture[0][context].WriteDataPitch(Camera.Data, Camera.GetSize(), 1);
                    else
                        FOutTexture[0][context].WriteData(Camera.Data, Camera.GetSize());
                }
            }

            //call when disconnect from renderer
            public void Destroy(DX11RenderContext context, bool force)
            {
                //FLogger.Log(LogType.Debug, "Destroy");

                FOutTexture[0].Dispose(context);
            }
        }
    }
}