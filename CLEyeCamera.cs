using System;
using System.Runtime.InteropServices;
using System.Windows;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

namespace PS3Eye
{
    #region [ Camera Parameters ]
    // camera color mode
    // MONO: 8 bit per pixel grayscale
    // COLOR: 32 bit per pixel RGBA
    public enum CLEyeCameraColorMode
    {
        CLEYE_MONO_PROCESSED,
        CLEYE_COLOR_PROCESSED,
        CLEYE_MONO_RAW,
        CLEYE_COLOR_RAW,
        //CLEYE_BAYER_RAW
    };

    // camera resolution
    public enum CLEyeCameraResolution
    {
        CLEYE_QVGA,                 // 320 x 240. Allowed frame rates: 15, 30, 60, 75, 100, 125
        CLEYE_VGA                   // 640 x 480, Allowed frame rates: 15, 30, 40, 50, 60, 75
    };

    // camera parameters
    public enum CLEyeCameraParameter
    {
        // camera sensor parameters
        CLEYE_AUTO_GAIN,			// [false, true]
        CLEYE_GAIN,					// [0, 79]
        CLEYE_AUTO_EXPOSURE,		// [false, true]
        CLEYE_EXPOSURE,				// [0, 511]
        CLEYE_AUTO_WHITEBALANCE,	// [false, true]
        CLEYE_WHITEBALANCE_RED,		// [0, 255]
        CLEYE_WHITEBALANCE_GREEN,	// [0, 255]
        CLEYE_WHITEBALANCE_BLUE,	// [0, 255]
        // camera linear transform parameters
        CLEYE_HFLIP,				// [false, true]
        CLEYE_VFLIP,				// [false, true]
        CLEYE_HKEYSTONE,			// [-500, 500]
        CLEYE_VKEYSTONE,			// [-500, 500]
        CLEYE_XOFFSET,				// [-500, 500]
        CLEYE_YOFFSET,				// [-500, 500]
        CLEYE_ROTATION,				// [-500, 500]
        CLEYE_ZOOM,					// [-500, 500]
        // camera non-linear transform parameters
        CLEYE_LENSCORRECTION1,		// [-500, 500]
        CLEYE_LENSCORRECTION2,		// [-500, 500]
        CLEYE_LENSCORRECTION3,		// [-500, 500]
        CLEYE_LENSBRIGHTNESS		// [-500, 500]
    };
    #endregion

    public class CLEyeCamera : DependencyObject, IDisposable
    {
        #region [ CLEyeMulticam Imports ]

        /*
        https://codelaboratories.com/research/view/cl-eye-muticamera-api
        
        int CLEyeGetCameraCount() returns CameraCount as int 
        Returns number of currently connected and detected cameras. 

        GUID CLEyeGetCameraUUID(CameraIndex as int) 
        Returns UUID: Camera unique identifier (UUID) for specified camera index. 

        CLEyeCameraInstance CLEyeCreateCamera(CamUIID as GUID, CLEyeCameraColorMode, CLEyeCameraResolution, Framerate as int) returns CLEyeCameraInstance 
        Creates CLEyeCameraInstance from the camera UUID and sets the desired camera color mode, resolution and frame rate. Returns CLEyeCameraInstance value of NULL if it fails. 

        bool CLEyeDestroyCamera(CLEyeCameraInstance) 
        Destroys specified CLEyeCameraInstance. Returns true if it succeeds and false if it fails. 

        bool CLEyeCameraStart(CLEyeCameraInstance) 
        Starts camera video capturing for specified camera instance. Returns true if it succeeds and false if it fails. 

        bool CLEyeCameraStop(CLEyeCameraInstance) 
        Stops camera video capturing for specified camera instance. Returns true if it succeeds and false if it fails. 

        bool CLEyeSetCameraParameter(CLEyeCameraInstance, CLEyeCameraParameter, Value as int) 
        Sets given camera parameter (See CLEyeCameraParameters for details) value for specified camera instance. Returns true if it succeeds and false if it fails. 

        int CLEyeGetCameraParameter(CLEyeCameraInstance, CLEyeCameraParameter) 
        Returns given camera parameter (See CLEyeCameraParameters for details) value for specified camera instance. 

        bool CLEyeCameraGetFrameDimensions(CLEyeCameraInstance, Width as int, Height as int) 
        Returns the video frame size for specified camera instance. Returns true if it succeeds and false if it fails. 

        bool CLEyeCameraGetFrame(CLEyeCameraInstance, ImageData as PBYTE, Timeout as int) 
        Waits at most Timeout number of milliseconds (default 2000ms) for the latest video frame. Returns true if it succeeds and false if it fails. If the function succeeds, it also returns video frame data from a specified camera instance.
        */

        [DllImport("CLEyeMulticam.dll")]
        public static extern int CLEyeGetCameraCount();
        [DllImport("CLEyeMulticam.dll")]
        public static extern Guid CLEyeGetCameraUUID(int camId);
        [DllImport("CLEyeMulticam.dll")]
        public static extern IntPtr CLEyeCreateCamera(Guid camUUID, CLEyeCameraColorMode mode, CLEyeCameraResolution res, float frameRate);
        [DllImport("CLEyeMulticam.dll")]
        public static extern bool CLEyeDestroyCamera(IntPtr camera);
        [DllImport("CLEyeMulticam.dll")]
        public static extern bool CLEyeCameraStart(IntPtr camera);
        [DllImport("CLEyeMulticam.dll")]
        public static extern bool CLEyeCameraStop(IntPtr camera);
        [DllImport("CLEyeMulticam.dll")]
        public static extern bool CLEyeCameraLED(IntPtr camera, bool on);
        [DllImport("CLEyeMulticam.dll")]
        public static extern bool CLEyeSetCameraParameter(IntPtr camera, CLEyeCameraParameter param, int value);
        [DllImport("CLEyeMulticam.dll")]
        public static extern int CLEyeGetCameraParameter(IntPtr camera, CLEyeCameraParameter param);
        [DllImport("CLEyeMulticam.dll")]
        public static extern bool CLEyeCameraGetFrameDimensions(IntPtr camera, ref int width, ref int height);
        [DllImport("CLEyeMulticam.dll")]
        public static extern bool CLEyeCameraGetFrame(IntPtr camera, IntPtr pData, int waitTimeout);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UnmapViewOfFile(IntPtr hMap);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);
        #endregion

        public const int CAMERA_MAX = 16;

        public static bool[] IsCreated = new bool[CAMERA_MAX];
        public static int CameraCount { get { return CLEyeGetCameraCount(); } }
        public static Guid CameraUUID(int id) { return CLEyeGetCameraUUID(id); }

        private IntPtr _map = IntPtr.Zero;
        private IntPtr _section = IntPtr.Zero;
        private IntPtr _camera = IntPtr.Zero;
        //private Thread _workerThread;

        #region [ Properties ]
        public int ID { get; set; }
        public bool Running { get; private set; }
        public float Framerate { get; set; }
        public CLEyeCameraColorMode ColorMode { get; set; }
        public CLEyeCameraResolution Resolution { get; set; }
        public IntPtr Data
        {
            get
            {
                return _map;
            }
            private set
            {
                _map = value;
            }
        }
        public bool AutoGain
        {
            get
            {
                if (_camera == null) return false;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_AUTO_GAIN) != 0;
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_AUTO_GAIN, value ? 1 : 0);
            }
        }
        public int Gain
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_GAIN);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_GAIN, value);
            }
        }
        public bool AutoExposure
        {
            get
            {
                if (_camera == null) return false;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_AUTO_EXPOSURE) != 0;
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_AUTO_EXPOSURE, value ? 1 : 0);
            }
        }
        public int Exposure
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_EXPOSURE);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_EXPOSURE, value);
            }
        }
        public bool AutoWhiteBalance
        {
            get
            {
                if (_camera == null) return true;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_AUTO_WHITEBALANCE) != 0;
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_AUTO_WHITEBALANCE, value ? 1 : 0);
            }
        }
        public int WhiteBalanceRed
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_WHITEBALANCE_RED);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_WHITEBALANCE_RED, value);
            }
        }
        public int WhiteBalanceGreen
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_WHITEBALANCE_GREEN);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_WHITEBALANCE_GREEN, value);
            }
        }
        public int WhiteBalanceBlue
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_WHITEBALANCE_BLUE);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_WHITEBALANCE_BLUE, value);
            }
        }
        public bool HorizontalFlip
        {
            get
            {
                if (_camera == null) return false;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_HFLIP) != 0;
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_HFLIP, value ? 1 : 0);
            }
        }
        public bool VerticalFlip
        {
            get
            {
                if (_camera == null) return false;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_VFLIP) != 0;
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_VFLIP, value ? 1 : 0);
            }
        }
        public int HorizontalKeystone
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_HKEYSTONE);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_HKEYSTONE, value);
            }
        }
        public int VerticalKeystone
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_VKEYSTONE);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_VKEYSTONE, value);
            }
        }
        public int XOffset
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_XOFFSET);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_XOFFSET, value);
            }
        }
        public int YOffset
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_YOFFSET);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_YOFFSET, value);
            }
        }
        public int Rotation
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_ROTATION);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_ROTATION, value);
            }
        }
        public int Zoom
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_ZOOM);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_ZOOM, value);
            }
        }
        public int LensCorrection1
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSCORRECTION1);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSCORRECTION1, value);
            }
        }
        public int LensCorrection2
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSCORRECTION2);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSCORRECTION2, value);
            }
        }
        public int LensCorrection3
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSCORRECTION3);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSCORRECTION3, value);
            }
        }
        public int LensBrightness
        {
            get
            {
                if (_camera == null) return 0;
                return CLEyeGetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSBRIGHTNESS);
            }
            set
            {
                if (_camera == null) return;
                CLEyeSetCameraParameter(_camera, CLEyeCameraParameter.CLEYE_LENSBRIGHTNESS, value);
            }
        }
        public bool LED
        {
            set
            {
                if (_camera == null) return;
                CLEyeCameraLED(_camera, value);
            }
        }
        #endregion

        public CLEyeCamera()
        {
            // set default values
            Framerate = 30;
            ColorMode = CLEyeCameraColorMode.CLEYE_COLOR_PROCESSED;
            //ColorMode = CLEyeCameraColorMode.CLEYE_MONO_PROCESSED;
            Resolution = CLEyeCameraResolution.CLEYE_VGA;

            Data = IntPtr.Zero;
        }

        ~CLEyeCamera()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if(disposing)
            {
                Stop();
            }

            Destroy();
        }

        public bool Create(Guid cameraGuid)
        {
            int w = 0, h = 0;

            _camera = CLEyeCreateCamera(cameraGuid, ColorMode, Resolution, Framerate);
            if (_camera == IntPtr.Zero) return false;

            CLEyeCameraGetFrameDimensions(_camera, ref w, ref h);

            if (ColorMode == CLEyeCameraColorMode.CLEYE_COLOR_PROCESSED || ColorMode == CLEyeCameraColorMode.CLEYE_COLOR_RAW)
            {
                uint imageSize = (uint)w * (uint)h * 4;
                // create memory section and map
                _section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, imageSize, null);
                _map = MapViewOfFile(_section, 0xF001F, 0, 0, imageSize);
                //BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(_section, w, h, PixelFormats.Bgr32, w * 4, 0) as InteropBitmap;
            }
            else
            {
                uint imageSize = (uint)w * (uint)h;
                // create memory section and map
                _section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, imageSize, null);
                _map = MapViewOfFile(_section, 0xF001F, 0, 0, imageSize);
                //BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(_section, w, h, PixelFormats.Gray8, w, 0) as InteropBitmap;
            }
            // Invoke event
            //if (BitmapReady != null) BitmapReady(this, null);
            //BitmapSource.Invalidate();
            return true;
        }

        public bool Destroy()
        {
            return CLEyeDestroyCamera(_camera);
        }

        public bool Start()
        {
            Running = true;

            return CLEyeCameraStart(_camera);

            //_workerThread = new Thread(new ThreadStart(CaptureThread));
            //_workerThread.Start();
        }

        public bool Stop()
        {
            if (!Running)
                return false;

            Running = false;

            return CLEyeCameraStop(_camera);

            //_workerThread.Join(1000);
        }

        public bool GetFrame()
        {
            if (_camera == null) return false;

            return CLEyeCameraGetFrame(_camera, _map, 500);
        }

        public int GetSize()
        {
            if (_camera == null) return 0;

            int w = 0, h = 0;
            CLEyeCameraGetFrameDimensions(_camera, ref w, ref h);

            if (ColorMode == CLEyeCameraColorMode.CLEYE_COLOR_PROCESSED || ColorMode == CLEyeCameraColorMode.CLEYE_COLOR_RAW)
                return w * h * 4;
            else
                return w * h;
        }

        /*
        void CaptureThread()
        {
            CLEyeCameraStart(_camera);
            int i = 0;
            while (_running)
            {
                if (CLEyeCameraGetFrame(_camera, _map, 500))
                {
                    if (!_running)  break;
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, (SendOrPostCallback)delegate
                    {
                        BitmapSource.Invalidate();
                    }, null);
                    i++;
                }
            }
            CLEyeCameraStop(_camera);
            CLEyeDestroyCamera(_camera);
        }
        */
    }
}