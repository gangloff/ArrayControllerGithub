using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using ATMCD32CS;

namespace ArrayDACControl
{
    class Andor
    {
        //Debug
        public Stopwatch stopwatch = new Stopwatch();

        //error message string
        public String sStatusMsg = String.Empty;

        AndorSDK MyCamera = new AndorSDK();
        int iXPixels = 0;
        int iYPixels = 0;

        // binning & image parameters (public)
        public int hbin = 1, vbin = 1, hstart = 1, vstart = 1, hend = 1000, vend = 1000;

        // exposure etc.
        public float fExposure = 0, fAccumTime = 0, fKineticTime = 0;

        // EM gain
        public int EMGain = 1;
        public int EMGainMin = 0, EMGainMax = 1;

        //image size
        public ulong xImageSize, yImageSize;

        //acquisition status
        int status = 0;

        // running boolean
        public bool running = false;

        // variable update flag
        public bool variableChangeFlag = false;

        // error flag
        public bool errorFlag = false;

        public int[] DataInt;
        public double[,] DataDouble;

        // Error Code variable
        uint uiErrorCode;

        public bool AppInitialize()
        {
            //reset message string
            sStatusMsg = String.Empty;

            bool initSuccess = false;

            int i_numCams = 0;
            MyCamera.GetAvailableCameras(ref i_numCams);

            if (i_numCams == 0)
            {
                sStatusMsg += "No Cameras Detected...\n";
            }

            for (int i = 0; i < i_numCams; ++i)
            {
                int i_handle = 0;
                uiErrorCode = MyCamera.GetCameraHandle(i, ref i_handle);
                uiErrorCode = MyCamera.SetCurrentCamera(i_handle);
                uiErrorCode = MyCamera.Initialize("");

                if (uiErrorCode != AndorSDK.DRV_SUCCESS)
                {
                    sStatusMsg += "Initialize ERROR...\n";
                }
                else
                {
                    sStatusMsg += "Initialize OK...\n";
                    initSuccess = true;
                }
            }
            return initSuccess;
        }

        private void AppGetDetector()
        {
            uiErrorCode = MyCamera.GetDetector(ref iXPixels, ref iYPixels);

            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                sStatusMsg += "Format ERROR: Can't read CCD format...\n";
            }
        }

        private void AppSetAcquisition(uint acqMode)
        {
            //Set acquisition mode: single=1, video=2
            uiErrorCode = MyCamera.SetAcquisitionMode((int) acqMode);

            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                    switch (uiErrorCode)
                    {
                        case AndorSDK.DRV_P1INVALID:
                            sStatusMsg += "Invalid acquisition mode...\n";
                            break;
                        case AndorSDK.DRV_ACQUIRING:
                            sStatusMsg += "Acquisition in progress...\n";
                            break;
                        default:
                            sStatusMsg += "Unknown Acquisition error...\n";
                            break;
                    }
            }
        }

        public void AppSetReadout()
        {
            uiErrorCode = MyCamera.SetImage(hbin, vbin, hstart, hend, vstart, vend);
            uiErrorCode = MyCamera.SetReadMode(4); //Full Image

            //init DataInt array
            initData();

            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                switch (uiErrorCode)
                {
                    case AndorSDK.DRV_P1INVALID:
                        sStatusMsg += "Invalid readout mode...\n";
                        break;
                    case AndorSDK.DRV_ERROR_ACK:
                        sStatusMsg += "Unable to communicate with card...\n";
                        break;
                    default:
                        sStatusMsg += "Unknown readout mode error...\n";
                        break;
                }
            }
        }

        public void AppSetTimings()
        {
            //get maximum exposure allowed
            float MaxExp = 5;
            uiErrorCode = MyCamera.GetMaximumExposure(ref MaxExp);
            //replace if needed
            if (fExposure > MaxExp) { fExposure = MaxExp; }
            //set exposure
            uiErrorCode = MyCamera.SetExposureTime(fExposure);
            
            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                sStatusMsg += "Set Exposure Time ERROR...\n";
            }
        }

        private void AppGetTimings()
        {
            uiErrorCode = MyCamera.GetAcquisitionTimings(ref fExposure, ref fAccumTime, ref fKineticTime);

            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                sStatusMsg += "Timings ERROR: Can't get timings...\n";
            }
        }

        private void AppGetEMGainRange()
        {
            uiErrorCode = MyCamera.GetEMGainRange(ref EMGainMin, ref EMGainMax);

            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                sStatusMsg += "Could not get EM Gain range...\n";
            }
        }

        public void AppSetEMGain()
        {
            if (EMGain > EMGainMax) EMGain = EMGainMax;
            if (EMGain < EMGainMin) EMGain = EMGainMin;

            uiErrorCode = MyCamera.SetEMCCDGain(EMGain);

            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                sStatusMsg += "Could not set EM Gain...\n";
            }
        }

        private void AppSetAcq()
        {
            uiErrorCode = MyCamera.SetADChannel(0);
            uiErrorCode = MyCamera.SetVSSpeed(0);
            uiErrorCode = MyCamera.SetHSSpeed(0, 0);
            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                sStatusMsg += "Acquisition ERROR ...\n";
                errorFlag = true;
            }
        }

        public void AppShutDown()
        {
            uiErrorCode = MyCamera.ShutDown();
            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                sStatusMsg += "Shutdown ERROR ...\n";
            }
        }

        int GetMean(ushort[] sDataInt, ulong TotalPixels)
        {
            ulong count = 0;
            for (int i = 0; i < (int)TotalPixels; i++)
            {
                count += (ulong)sDataInt[i];
            }
            return (int)(count / TotalPixels);
        }

        public void AppSetSequence()
        {
            AppGetDetector();
            AppSetAcquisition(AndorSDK.AC_ACQMODE_VIDEO);
            AppSetReadout();
            AppSetTimings();
            AppGetTimings();
            AppGetEMGainRange();
            AppSetEMGain();
            AppSetAcq();
        }

        public void StartAcquisition()
        {
            uiErrorCode = MyCamera.StartAcquisition();

            switch (uiErrorCode)
            {
                case AndorSDK.DRV_SUCCESS:
                    sStatusMsg += "Acquisition started...\n";
                    break;
                case AndorSDK.DRV_NOT_INITIALIZED:
                    sStatusMsg += "System not initialized...\n";
                    break;
                case AndorSDK.DRV_ACQUIRING:
                    sStatusMsg += "Acquisition in progress...\n";
                    break;
                case AndorSDK.DRV_VXDNOTINSTALLED:
                    sStatusMsg += "VxD not loaded...\n";
                    break;
                case AndorSDK.DRV_ERROR_ACK:
                    sStatusMsg += "Unable to communicate with card...\n";
                    break;
                case AndorSDK.DRV_INIERROR:
                    sStatusMsg += "Error reading “DETECTOR.INI”...\n";
                    break;
                case AndorSDK.DRV_ACQUISITION_ERRORS:
                    sStatusMsg += "Acquisition settings invalid...\n";
                    break;
                case AndorSDK.DRV_ERROR_PAGELOCK:
                    sStatusMsg += "Unable to allocate memory...\n";
                    break;
                case AndorSDK.DRV_INVALID_FILTER:
                    sStatusMsg += "Filter not available for current acquisition...\n";
                    break;
            }
        }

        public void WaitForAcquition()
        {
            uiErrorCode = MyCamera.WaitForAcquisition();

            switch (uiErrorCode)
            {
                case AndorSDK.DRV_SUCCESS:
                    sStatusMsg += "Acquisition Event occurred...\n";
                    break;
                case AndorSDK.DRV_NOT_INITIALIZED:
                    sStatusMsg += "System not initialized...\n";
                    break;
                case AndorSDK.DRV_NO_NEW_DATA:
                    sStatusMsg += "Non-Acquisition Event occurred...\n";
                    break;
            }
        }


        public bool GetStatus()
        {
            uiErrorCode = MyCamera.GetStatus(ref status);

            if (status == AndorSDK.DRV_ACQUIRING)
                return true;
            else
                return false;
        }

        public void GetAcquiredData()
        {
            ulong TotalPixels = xImageSize * yImageSize;

            uiErrorCode = MyCamera.GetAcquiredData(DataInt, (uint)TotalPixels);

            if (uiErrorCode != AndorSDK.DRV_SUCCESS)
            {
                if (uiErrorCode == AndorSDK.DRV_P2INVALID)
                {
                    sStatusMsg += "ERROR: aDataInt size is too small...\n";
                }
                else
                {
                    sStatusMsg += "Acquisition error...\n";
                }
                errorFlag = true;
            }
            else
            {
                //set running flag to true
                running = true;
            }

            //convert data to double
            if (!errorFlag)
            {
                dataConvert();
            }
        }


        public void GetSingleAcquisition()
        {
            ulong TotalPixels = xImageSize*yImageSize;

            // Tell camera to start acquiring
            StartAcquisition();
            //stopwatch.Reset();
            //stopwatch.Start();
            WaitForAcquisition();
            //stopwatch.Stop();
            uiErrorCode = MyCamera.GetAcquiredData(DataInt, (uint)TotalPixels);
            //uiErrorCode = MyCamera.GetOldestImage(DataInt, (uint)TotalPixels);

            switch (uiErrorCode)
            {
                case AndorSDK.DRV_SUCCESS:
                    sStatusMsg += "Data copied...\n";
                    running = true;
                    break;
                case AndorSDK.DRV_NOT_INITIALIZED:
                    sStatusMsg += "System not initialized...\n";
                    errorFlag = true;
                    break;
                case AndorSDK.DRV_ACQUIRING:
                    sStatusMsg += "Acquisition in progress...\n";
                    break;
                case AndorSDK.DRV_ERROR_ACK:
                    sStatusMsg += "Unable to communicate with card...\n";
                    errorFlag = true;
                    break;
                case AndorSDK.DRV_INIERROR:
                    sStatusMsg += "Error reading “DETECTOR.INI”...\n";
                    errorFlag = true;
                    break;
                case AndorSDK.DRV_P1INVALID:
                    sStatusMsg += "Invalid pointer (i.e. NULL)...\n";
                    errorFlag = true;
                    break;
                case AndorSDK.DRV_P2INVALID:
                    sStatusMsg += "Array size is incorrect...\n";
                    errorFlag = true;
                    break;
                case AndorSDK.DRV_NO_NEW_DATA:
                    sStatusMsg += "No acquisition has taken place...\n";
                    break;
            }

            //convert data to double
            if (!errorFlag)
            {
                dataConvert();
            }
        }

        public int GetTemperature()
        {
            int temp = 0;
            uiErrorCode = MyCamera.GetTemperature(ref temp);

            switch (uiErrorCode)
            {
                case AndorSDK.DRV_NOT_INITIALIZED:
                    sStatusMsg += "System not initialized...\n";
                    break;
                case AndorSDK.DRV_ACQUIRING:
                    sStatusMsg += "Acquisition in progress...\n";
                    break;
                case AndorSDK.DRV_ERROR_ACK:
                    sStatusMsg += "Unable to communicate with card...\n";
                    break;
                case AndorSDK.DRV_TEMP_OFF:
                    sStatusMsg += "Temperature is OFF...\n";
                    break;
                case AndorSDK.DRV_TEMP_STABILIZED:
                    sStatusMsg += "Temperature has stabilized at set point...\n";
                    break;
                case AndorSDK.DRV_TEMP_NOT_REACHED:
                    sStatusMsg += "Temperature has not reached set point...\n";
                    break;
                case AndorSDK.DRV_TEMP_DRIFT:
                    sStatusMsg += "Temperature had stabilized but has since drifted...\n";
                    break;
                case AndorSDK.DRV_TEMP_NOT_STABILIZED:
                    sStatusMsg += "Temperature reached but not stabilized...\n";
                    break;
            }

            return temp;
        }


        public void WaitForAcquisition()
        {
            MyCamera.WaitForAcquisition();
        }

        public void Abort()
        {
            MyCamera.AbortAcquisition();
            running = false;
        }

        private void initData()
        {
            xImageSize = (ulong)Math.Ceiling((double)Math.Abs(hend - hstart + 1) / hbin);
            yImageSize = (ulong)Math.Ceiling((double)Math.Abs(vend - vstart + 1) / vbin);
            ulong TotalPixels = xImageSize * yImageSize;
            DataInt = new int[TotalPixels];
            DataDouble = new double[xImageSize,yImageSize];
        }

        private void dataConvert()
        {
            variableChangeFlag = true;

            if ((ulong) DataDouble.Length >= xImageSize * yImageSize)
            {
                for (ulong i = 0; i < yImageSize; i++)
                {
                    for (ulong j = 0; j < xImageSize; j++)
                    {
                        DataDouble[j, i] = (double)DataInt[i * xImageSize + j];
                    }
                }
            }

            variableChangeFlag = false;
        }
    }
}
