﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using NationalInstruments.DAQmx;
using NationalInstruments.UI.WindowsForms;



namespace ArrayDACControl
{
    public partial class Form1 : Form
    {
        DACController DAC;
        NICardController Dev4AO0, Dev4AO1, Dev4AO2, Dev4AO3, Dev4AO4, Dev4AO5, Dev4AO6, Dev4AO7, Dev7AO0, Dev7AO2, Dev7AO6, Dev7AO7;
        NICardController Dev2DO0, Dev2DO1, Dev2DO2, Dev2DO3, Dev2DO4, Dev2DO5, Dev2DO6, Dev2DO7;
        NICardController Dev3AI2;
        GPIB gpibdevice;
        Andor Camera;
        Form2 CameraForm;
        Correlator theCorrelator;

        Stopwatch stopwatch;
        int ncorrbins;
        //double[] corrbins = new double[26] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 };



        int counterMu = 0;
        int counterAmp = 0;

        int avgCount = 0;

        //Calibration values
        double BxCalib = -0.78; //measured by Dorian & Leon Mar 6, 2012
        double RepumperCalib = 258.4; //slider value to frequency conversion
        double TCcalib = 692; //slider value to frequency conversion for transfer cavity
        double SizeOfPixel = 2160; //size of imaged system projected onto one pixel in nm
        double TickleCalib = 1;

        //thread definitions
        Thread CameraFormThread;
        //thread helper classes
        ThreadHelperClass SliderScanThreadHelper, ElectrodeScanThreadHelper;
        ThreadHelperClass CameraThreadHelper, CameraTimeOutThreadHelper, IntensityGraphUpdateThreadHelper;
        ThreadHelperClass CorrelatorThreadHelper, SinglePMTReadThreadHelper, FluorLogThreadHelper;
        delegate void MyDelegate();

        double BL, BR, TL, TR, midL, midR;

        //adjusted apr 10, 2011 by Dorian & Alexei based on updated calculations
        //const double arrayratio = -0.786;
        const double arrayratio = -0.152;
        const double traplength = 10000;
        const int nplotpoints = 1000;


        double[] electrodeZ = new double[12] { -4590, -3360, -2400, -1440, -720, -240, 240, 720, 1440, 2400, 3360, 4590 };
        double[] electrodeWz = new double[12] { 1500, 960, 960, 960, 480, 480, 480, 480, 960, 960, 960, 1500 };
        const double xdisp = 640;//640
        const double wx = 745;
        const double ytrap = 140;//140

        double[,] danceWaveform;
        const int danceSampleRate = 1000;
        const int danceDCrow = 4;


        double vertbias, outerL, outerR, innerL, innerC, innerR;
        double DX;

        private Dictionary<int, List<int>> wireToChannel;
        private Dictionary<int, List<int>> channelToWire;
        private Dictionary<int, List<int>> rowToWire;
        private Dictionary<int, List<int>> wireToRow;
        private Dictionary<int, List<int>> rowToChannel;
        private Dictionary<int, List<int>> channelToRow;
        private Dictionary<int, List<int>> columnToChannel;
        private Dictionary<int, List<int>> channelToColumn;
        private Dictionary<int, List<int>> columnToWire;
        private Dictionary<int, List<int>> wireToColumn;

        private Dictionary<String, int> RFelectrodeToChannel;

        private int[,] rowColumnToChannel;

        private Label[,] DCindicators;

        const int DCrows = 12;
        AdjustableSlider[] DCsliders;
        AdjustableSlider[] DCslidersDx;
        AdjustableSlider[] DCslidersLeft;
        AdjustableSlider[] DCslidersRight;
        double[] DCvalues;
        double[] DCvaluesDx;
        double[] DCvaluesLeft;
        double[] DCvaluesRight;
        double[] DCvoltagesZ;

        private Dictionary<int, List<int>> reverseDictionary(Dictionary<int, List<int>> dict)
        {
            Dictionary<int, List<int>> newDict = new Dictionary<int, List<int>>();
            List<int> NewKeys = new List<int>();

            // find all values mentioned in dict
            foreach (List<int> val in dict.Values)
                foreach (int val2 in val)
                    if (!NewKeys.Contains(val2))
                        NewKeys.Add(val2);

            // for each value mentioned in dict
            // find all keys that mention it
            foreach (int nk in NewKeys)
            {
                List<int> keys = new List<int>();
                foreach (int key in dict.Keys)
                    if (dict[key].Contains(nk))
                        keys.Add(key);
                newDict.Add(nk, keys);
            }
            return newDict;
        }
        // result will link keys in a with values in b reachable via a

        //
        // INITIAL FORM INITIALIZATIONS
        //
        public Form1()
        {
            DCindicators = new Label[2, DCrows];
            DCsliders = new AdjustableSlider[DCrows];
            DCslidersDx = new AdjustableSlider[DCrows];
            DCslidersLeft = new AdjustableSlider[DCrows];
            DCslidersRight = new AdjustableSlider[DCrows];

            for (int i = 0; i < DCsliders.Length; i++)
                DCsliders[i] = new AdjustableSlider();
            for (int i = 0; i < DCslidersDx.Length; i++)
                DCslidersDx[i] = new AdjustableSlider();
            for (int i = 0; i < DCslidersLeft.Length; i++)
                DCslidersLeft[i] = new AdjustableSlider();
            for (int i = 0; i < DCslidersRight.Length; i++)
                DCslidersRight[i] = new AdjustableSlider();

            DCvalues = new double[DCrows];
            DCvaluesDx = new double[DCrows];
            DCvaluesLeft = new double[DCrows];
            DCvaluesRight = new double[DCrows];
            DCvoltagesZ = new double[DCrows];

            InitializeComponent();
            initializeSliders();

            DAC = new DACController();
            // Raman VVA
            Dev4AO0 = new NICardController();
            Dev4AO0.InitAnalogOutput("Dev4/ao0", 0, 10);
            // Repumper Color
            Dev4AO1 = new NICardController();
            Dev4AO1.InitAnalogOutput("Dev4/ao1", 0, 10); //Repumper HV amp input saturates at 7V
            // 370 Current Feedforward
            //Dev4AO2 = new NICardController();
            //Dev4AO2.InitAnalogOutput("Dev4/ao2", 0, 10);
            // Transfer Cavity Piezo
            Dev4AO3 = new NICardController();
            Dev4AO3.InitAnalogOutput("Dev4/ao3", 0, 10);
            // Repumper Power Control
            Dev4AO4 = new NICardController();
            Dev4AO4.InitAnalogOutput("Dev4/ao4", 0, 10);
            // Side Beam 370 Power
            Dev4AO5 = new NICardController();
            Dev4AO5.InitAnalogOutput("Dev4/ao5", 0, 10);
            // 402 Sideband VCO
            Dev4AO6 = new NICardController();
            Dev4AO6.InitAnalogOutput("Dev4/ao6", 0, 10);
            // Bx control
            Dev7AO2 = new NICardController();
            Dev7AO2.InitAnalogOutput("Dev7/ao2", -7, 7);
            // Tickle control
            Dev7AO0 = new NICardController();
            Dev7AO0.InitAnalogOutput("Dev7/ao0", -5, 5);
            // 402 Sideband VVA
            Dev4AO7 = new NICardController();
            Dev4AO7.InitAnalogOutput("Dev4/ao7", 0, 10);
            // Lattice Power Control
            Dev7AO6 = new NICardController();
            Dev7AO6.InitAnalogOutput("Dev7/ao6", 0, 10);
            // APD Bias
            Dev7AO7 = new NICardController();
            Dev7AO7.InitAnalogOutput("Dev7/ao7", 0, 10);
            // Ionization (399) Shutter
            Dev2DO0 = new NICardController();
            Dev2DO0.InitDigitalOutput("Dev2/port2/line0");
            // 370 Cavity Cooling Beam Shutter
            Dev2DO1 = new NICardController();
            Dev2DO1.InitDigitalOutput("Dev2/port2/line1");
            // Repumper RF Broaden Switch
            Dev2DO2 = new NICardController();
            Dev2DO2.InitDigitalOutput("Dev2/port2/line2");
            // Lattice switch
            Dev2DO3 = new NICardController();
            Dev2DO3.InitDigitalOutput("Dev2/port2/line3");
            // Correlator Lock-In frequency switch
            Dev2DO4 = new NICardController();
            Dev2DO4.InitDigitalOutput("Dev2/port2/line4");
            // Lattice Tickle Switch
            Dev2DO5 = new NICardController();
            Dev2DO5.InitDigitalOutput("Dev2/port2/line5");
            // 638 Shutter
            Dev2DO6 = new NICardController();
            Dev2DO6.InitDigitalOutput("Dev2/port2/line6");
            // Camera Trigger
            Dev2DO7 = new NICardController();
            Dev2DO7.InitDigitalOutput("Dev2/port2/line7");
            // Analog Input
            Dev3AI2 = new NICardController();
            Dev3AI2.InitAnalogInput("Dev3/ai2", -10, 10);

            //Initialize GPIB communication interface
            gpibdevice = new GPIB(0, (byte)3);

            // sometimes, the DAC ignores the first output command after a reset
            // so, update all electrodes TWICE to make sure everything is in sync
            compensationAdjustedHelper();
            compensationAdjustedHelper();

            //Threading Definitions
            Thread.CurrentThread.Name = "Main thread";
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.Normal;

            //Camera Form Thread
            /*
            CameraFormThread = new Thread(new ThreadStart(CameraFormThreadExecute));
            CameraFormThread.Name = "Camera Form thread";
            CameraFormThread.Priority = ThreadPriority.BelowNormal;
            CameraFormThread.Start();
             */

            CameraForm = new Form2();
            CameraForm.Show();

            //Camera
            Camera = new Andor();

            //Correlator
            theCorrelator = new Correlator();

            //ThreadHelper classes
            CameraThreadHelper = new ThreadHelperClass("Camera");
            CameraTimeOutThreadHelper = new ThreadHelperClass("CameraTimeOut");
            SinglePMTReadThreadHelper = new ThreadHelperClass("SinglePMTRead");
            ElectrodeScanThreadHelper = new ThreadHelperClass("ElectrodeScan");
            IntensityGraphUpdateThreadHelper = new ThreadHelperClass("IntensityGraphUpdate");
            CorrelatorThreadHelper = new ThreadHelperClass("CorrelatorThread");
            FluorLogThreadHelper = new ThreadHelperClass("FluorLog");
            SliderScanThreadHelper = new ThreadHelperClass("SliderScan");

            stopwatch = new Stopwatch();
        }


        //
        // HELPER FUNCTIONS
        //

        private Dictionary<int, List<int>> composeDictionaries(Dictionary<int, List<int>> a, Dictionary<int, List<int>> b)
        {
            Dictionary<int, List<int>> result = new Dictionary<int, List<int>>();

            foreach (int key in a.Keys)
            {
                List<int> newvals = new List<int>();

                foreach (int val in a[key])
                    foreach (int val2 in b[val])
                        if (!newvals.Contains(val2))
                            newvals.Add(val2);

                result.Add(key, newvals);
            }

            return result;
        }
        private void initializeDictionaries()
        {
            //////////////////////////////////////////////////////
            // Alexei's notebook #2, pg. 16 
            //////////////////////////////////////////////////////
            // The first # is the original electrode numbering as shown in Marko's thesis. The second # is the DAC channel.                        
            Dictionary<int, int> wireToChannelHelper = new Dictionary<int, int>();
            wireToChannelHelper.Add(1, 12);
            wireToChannelHelper.Add(2, 10);
            wireToChannelHelper.Add(3, 9);
            wireToChannelHelper.Add(4, 11);
            wireToChannelHelper.Add(6, 15);
            wireToChannelHelper.Add(7, 13);
            wireToChannelHelper.Add(8, 16);
            wireToChannelHelper.Add(9, 14);
            wireToChannelHelper.Add(10, 3);
            wireToChannelHelper.Add(11, 6);
            wireToChannelHelper.Add(12, 1);
            wireToChannelHelper.Add(13, 2);
            wireToChannelHelper.Add(14, 5);
            wireToChannelHelper.Add(15, 4);
            wireToChannelHelper.Add(16, 8);
            wireToChannelHelper.Add(17, 7);
            wireToChannelHelper.Add(18, 24);
            wireToChannelHelper.Add(19, 23);
            wireToChannelHelper.Add(20, 22);
            wireToChannelHelper.Add(21, 21);
            wireToChannelHelper.Add(22, 19);
            wireToChannelHelper.Add(23, 20);
            wireToChannelHelper.Add(25, 18);
            wireToChannelHelper.Add(26, 17);
            wireToChannel = new Dictionary<int, List<int>>();
            foreach (int nk in wireToChannelHelper.Keys)
            {
                wireToChannel.Add(nk, new List<int>(new int[] { wireToChannelHelper[nk] }));
            }
            channelToWire = reverseDictionary(wireToChannel);

            rowToWire = new Dictionary<int, List<int>>();

            rowToWire.Add(0, new List<int>(new int[] { 17, 18 }));
            rowToWire.Add(1, new List<int>(new int[] { 16, 19 }));
            rowToWire.Add(2, new List<int>(new int[] { 15, 20 }));
            rowToWire.Add(3, new List<int>(new int[] { 14, 21 }));
            rowToWire.Add(4, new List<int>(new int[] { 13, 22 }));
            rowToWire.Add(5, new List<int>(new int[] { 12, 23 }));
            rowToWire.Add(6, new List<int>(new int[] { 11, 25 }));
            rowToWire.Add(7, new List<int>(new int[] { 10, 26 }));
            // The electrodes are flipped in the next 4 lines
            rowToWire.Add(11, new List<int>(new int[] { 9, 1 }));
            rowToWire.Add(10, new List<int>(new int[] { 8, 2 }));
            rowToWire.Add(9, new List<int>(new int[] { 7, 3 }));
            rowToWire.Add(8, new List<int>(new int[] { 6, 4 }));
            wireToRow = reverseDictionary(rowToWire);

            columnToWire = new Dictionary<int, List<int>>();
            columnToWire.Add(0, new List<int>(new int[] { 18, 19, 20, 21, 22, 23, 25, 26, 1, 2, 3, 4 }));
            columnToWire.Add(1, new List<int>(new int[] { 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6 }));
            wireToColumn = reverseDictionary(columnToWire);

            RFelectrodeToChannel = new Dictionary<string, int>();
            RFelectrodeToChannel.Add("outerL", 28);
            RFelectrodeToChannel.Add("outerR", 32);
            RFelectrodeToChannel.Add("innerL", 31);
            RFelectrodeToChannel.Add("snake", 30);
            RFelectrodeToChannel.Add("innerR", 29);

            columnToChannel = composeDictionaries(columnToWire, wireToChannel);
            channelToColumn = reverseDictionary(columnToChannel);

            rowToChannel = composeDictionaries(rowToWire, wireToChannel);
            channelToRow = reverseDictionary(rowToChannel);

            rowColumnToChannel = new int[DCrows, 2];

            foreach (int ch in channelToColumn.Keys)
            {
                rowColumnToChannel[channelToRow[ch][0], channelToColumn[ch][0]] = ch;
            }
            //////////////////////////////////////////////////////

            DCindicators[0, 0] = this.label12;
            DCindicators[0, 1] = this.label11;
            DCindicators[0, 2] = this.label10;
            DCindicators[0, 3] = this.label9;
            DCindicators[0, 4] = this.label8;
            DCindicators[0, 5] = this.label7;
            DCindicators[0, 6] = this.label6;
            DCindicators[0, 7] = this.label5;
            DCindicators[0, 8] = this.label4;
            DCindicators[0, 09] = this.label3;
            DCindicators[0, 10] = this.label2;
            DCindicators[0, 11] = this.label1;

            DCindicators[1, 0] = this.label13;
            DCindicators[1, 1] = this.label14;
            DCindicators[1, 2] = this.label15;
            DCindicators[1, 3] = this.label16;
            DCindicators[1, 4] = this.label17;
            DCindicators[1, 5] = this.label18;
            DCindicators[1, 6] = this.label19;
            DCindicators[1, 7] = this.label22;
            DCindicators[1, 8] = this.label20;
            DCindicators[1, 09] = this.label21;
            DCindicators[1, 10] = this.label23;
            DCindicators[1, 11] = this.label24;
        }

        private void initializeSliders()
        {
            initializeDictionaries();

            for (int i = 0; i < DCrows; i++)
            {
                DCsliders[i].SliderAdjusted += this.compensationAdjusted;
                DCsliders[i].Size = new System.Drawing.Size(400, 75);
                DCsliders[i].TabIndex = 90 + i;
                DCsliders[i].Location = new System.Drawing.Point(0, 80 + (70) * ((DCrows - i - 1) - 1));
                DCsliders[i].Name = String.Format("DC {0}", i);
                DCsliders[i].SliderLabel = String.Format("DC {0}", i);
                DCsliders[i].Min = -10;
                DCsliders[i].Max = 10;
                DCsliders[i].AbsMin = -20;
                DCsliders[i].AbsMax = 20;
                DCsliders[i].Value = 0;
                this.Controls.Add(DCsliders[i]);
                this.CoupledDCTab.Controls.Add(this.DCsliders[i]);
            }

            for (int i = 0; i < DCrows; i++)
            {
                DCslidersDx[i].SliderAdjusted += this.compensationAdjusted;
                DCslidersDx[i].Size = new System.Drawing.Size(400, 75);
                DCslidersDx[i].TabIndex = 90 + i;
                DCslidersDx[i].Location = new System.Drawing.Point(0, 80 + (70) * ((DCrows - i - 1) - 1));
                DCslidersDx[i].Name = String.Format("DC dx {0}", i);
                DCslidersDx[i].SliderLabel = String.Format("DC dx {0}", i);
                DCslidersDx[i].Min = -1;
                DCslidersDx[i].Max = +1;
                DCslidersDx[i].AbsMin = -20;
                DCslidersDx[i].AbsMax = 20;
                DCslidersDx[i].Value = 0;
                this.Controls.Add(DCslidersDx[i]);
                this.IndividualDCTab.Controls.Add(this.DCslidersDx[i]);
            }

            for (int i = 0; i < DCrows; i++)
            {
                DCslidersLeft[i].SliderAdjusted += this.compensationAdjusted;
                DCslidersLeft[i].Size = new System.Drawing.Size(400, 75);
                DCslidersLeft[i].TabIndex = 90 + i;
                DCslidersLeft[i].Location = new System.Drawing.Point(400, 80 + (70) * ((DCrows - i - 1) - 1));
                DCslidersLeft[i].Name = String.Format("DC left {0}", i);
                DCslidersLeft[i].SliderLabel = String.Format("DC left {0}", i);
                DCslidersLeft[i].Min = -10;
                DCslidersLeft[i].Max = +10;
                DCslidersLeft[i].AbsMin = -20;
                DCslidersLeft[i].AbsMax = 20;
                DCslidersLeft[i].Value = 0;
                this.Controls.Add(DCslidersLeft[i]);
                this.IndividualDCTab.Controls.Add(this.DCslidersLeft[i]);
            }

            for (int i = 0; i < DCrows; i++)
            {
                DCslidersRight[i].SliderAdjusted += this.compensationAdjusted;
                DCslidersRight[i].Size = new System.Drawing.Size(400, 75);
                DCslidersRight[i].TabIndex = 90 + i;
                DCslidersRight[i].Location = new System.Drawing.Point(800, 80 + (70) * ((DCrows - i - 1) - 1));
                DCslidersRight[i].Name = String.Format("DC right {0}", i);
                DCslidersRight[i].SliderLabel = String.Format("DC right {0}", i);
                DCslidersRight[i].Min = -10;
                DCslidersRight[i].Max = +10;
                DCslidersRight[i].AbsMin = -20;
                DCslidersRight[i].AbsMax = +20;
                DCslidersRight[i].Value = 0;
                this.Controls.Add(DCslidersRight[i]);
                this.IndividualDCTab.Controls.Add(this.DCslidersRight[i]);
            }

            this.DXSlider.SliderAdjusted += this.compensationAdjusted;
            this.ArrayTotalSlider.SliderAdjusted += this.compensationAdjusted;
            this.DCVertDipoleSlider.SliderAdjusted += this.compensationAdjusted;
            this.DCVertQuadSlider.SliderAdjusted += this.compensationAdjusted;
            this.TotalBiasSlider.SliderAdjusted += this.compensationAdjusted;
            this.TrapHeightSlider.SliderAdjusted += this.compensationAdjusted;
            this.QuadrupoleTilt.SliderAdjusted += this.compensationAdjusted;
            this.QuadTiltRatioSlider.SliderAdjusted += this.compensationAdjusted;
            this.SnakeRatioSlider.SliderAdjusted += this.compensationAdjusted;
            this.RightFingersSlider.SliderAdjusted += this.compensationAdjusted;
            this.LeftFingersSlider.SliderAdjusted += this.compensationAdjusted;
            this.SnakeOnlySlider.SliderAdjusted += this.compensationAdjusted;
            this.TransferCavity.SliderAdjusted += this.TransferCavityOut;
            this.RepumperSlider.SliderAdjusted += this.RepumperSliderOut;
            this.RepumperPowerSlider.SliderAdjusted += this.RepumperPowerSliderOut;
            this.SideBeam370Power.SliderAdjusted += this.SideBeam370PowerOut;
            this.LatticePowerControl.SliderAdjusted += this.LatticePowerControlOut;
            this.CavityCoolingPowerControl.SliderAdjusted += this.APDBiasOut;
            this.Sideband402Control.SliderAdjusted += this.Sideband402ControlOut;
            this.RamanSlider.SliderAdjusted += this.RamanSliderOut;
            this.BxSlider.SliderAdjusted += this.BxSliderOut;
            this.TickleSlider.SliderAdjusted += this.TickleSliderOut;
            this.CorrelatorBinningPhaseSlider.SliderAdjusted += this.CorrelatorBinningPhaseSliderOut;

            this.DXSlider.Value = 0;
        }

        private void UpdateAll()
        {
            compensationAdjustedHelper();
            RepumperSliderOutHelper();
            BxSliderOutHelper();
            TickleSliderOutHelper();
            RepumperPowerSliderOutHelper();
            //Dev4AO2.OutputAnalogValue((double)(TransferCavity.Value - CurrentFeedforward370Offset.Value) * CurrentFeedforward370Gain.Value / TCcalib);
            Dev4AO3.OutputAnalogValue((double)TransferCavity.Value / TCcalib);
            Dev4AO4.OutputAnalogValue((double)RepumperPowerSlider.Value);
            Dev4AO5.OutputAnalogValue((double)SideBeam370Power.Value);
            Dev7AO6.OutputAnalogValue((double)LatticePowerControl.Value);
            Dev7AO7.OutputAnalogValue((double)CavityCoolingPowerControl.Value);
            Dev2DO0.OutputDigitalValue(IonizationShutter.Value);
            Dev2DO1.OutputDigitalValue(CavityBeam370Switch.Value);
        }

        public void compensationAdjusted(object sender, EventArgs e)
        {
            compensationAdjustedHelper();
        }
        private void compensationAdjustedHelper()
        {

            const double h = 134;
            const double Win = 1.01 * h;
            const double Wout = 1.48 * h;
            const double r1 = 0.505 * h;
            const double r2 = 1.985 * h;

            double y0 = TrapHeightSlider.Value;

            double dipoleOut = 1;
            double dipoleIn = 1 - r2 * (r1 * r1 + y0 * y0) * (r1 * r1 + y0 * y0) / (r1 * (r2 * r2 + y0 * y0) * (r2 * r2 + y0 * y0));
            double quadOut = 1;
            double quadIn = -(y0 * y0 - h * h) / (y0 * y0 * r1 + h * h * r2) * Wout;

            double vertbias = TotalBiasSlider.Value;

            //changed apr 10, 2011; removed quad contribution from inner to avoid decompensation. Removed coarse DC vertical dipole control since total bias is sufficient
            double innerV = 0 * dipoleIn * DCVertDipoleSlider.Value + quadIn * DCVertQuadSlider.Value + vertbias;
            double outerV = 0 * dipoleOut * DCVertDipoleSlider.Value + quadOut * DCVertQuadSlider.Value + vertbias;

            //double innerV = vertbias;
            //double outerV = quadOut * DCVertQuadSlider.Value + vertbias;

            innerC = ArrayTotalSlider.Value + innerV + SnakeOnlySlider.Value;
            //changed apr 10, 2011; temporary slider for experimental ratio adjustement
            //innerL = arrayratio * ArrayTotalSlider.Value + innerV;
            //innerR = arrayratio * ArrayTotalSlider.Value + innerV;  
            //changed jan 23, 2013; quad tilt applied from outers
            //innerL = RatioSlider.Value * ArrayTotalSlider.Value + innerV + QuadTiltRatioSlider.Value * QuadrupoleTilt.Value + LeftFingersSlider.Value;
            //innerR = RatioSlider.Value * ArrayTotalSlider.Value + innerV - QuadTiltRatioSlider.Value * QuadrupoleTilt.Value + RightFingersSlider.Value;
            innerL = SnakeRatioSlider.Value * ArrayTotalSlider.Value + innerV + LeftFingersSlider.Value;
            innerR = SnakeRatioSlider.Value * ArrayTotalSlider.Value + innerV + RightFingersSlider.Value;
            //outers = outerV;
            outerL = outerV + QuadrupoleTilt.Value;
            outerR = outerV - QuadrupoleTilt.Value;
            

            DX = DXSlider.Value;

            for (int i = 0; i < DCrows; i++)
                this.DCvalues[i] = DCsliders[i].Value + vertbias;

            for (int i = 0; i < DCrows; i++)
                this.DCvaluesDx[i] = DCslidersDx[i].Value;
            for (int i = 0; i < DCrows; i++)
                this.DCvaluesLeft[i] = DCslidersLeft[i].Value;
            for (int i = 0; i < DCrows; i++)
                this.DCvaluesRight[i] = DCslidersRight[i].Value;

            DAC.Clear();
            for (int i = 0; i < DCrows; i++)
            {
                int channelL = rowColumnToChannel[i, 0];
                int channelR = rowColumnToChannel[i, 1];

                try
                {
                    //DAC.outputOneOffsetElectrodeVoltage((uint)channelL, -DX / 2 + DCvalues[i] - DCvaluesDx[i] + DCvaluesLeft[i] + QuadrupoleTilt.Value);
                    DAC.outputOneOffsetElectrodeVoltage((uint)channelL, -DX / 2 + DCvalues[i] - DCvaluesDx[i] + DCvaluesLeft[i] + QuadTiltRatioSlider.Value * QuadrupoleTilt.Value);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
                try
                {
                    //DAC.outputOneOffsetElectrodeVoltage((uint)channelR, DX / 2 + DCvalues[i] + DCvaluesDx[i] + DCvaluesRight[i] - QuadrupoleTilt.Value);
                    DAC.outputOneOffsetElectrodeVoltage((uint)channelR, DX / 2 + DCvalues[i] + DCvaluesDx[i] + DCvaluesRight[i] - QuadTiltRatioSlider.Value * QuadrupoleTilt.Value);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                DCvoltagesZ[i] = DCvalues[i];
            }
            showZPotential(DCvoltagesZ, nplotpoints);

            try
            {
                DAC.outputOneOffsetElectrodeVoltage((uint)this.RFelectrodeToChannel["outerL"], outerL);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            try
            {
                DAC.outputOneOffsetElectrodeVoltage((uint)this.RFelectrodeToChannel["outerR"], outerR);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            try
            {
                DAC.outputOneOffsetElectrodeVoltage((uint)this.RFelectrodeToChannel["innerL"], innerL);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            try
            {
                DAC.outputOneOffsetElectrodeVoltage((uint)this.RFelectrodeToChannel["innerR"], innerR);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            try
            {
                DAC.outputOneOffsetElectrodeVoltage((uint)this.RFelectrodeToChannel["snake"], innerC);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            // commit data to DAC
            DAC.Commit();
            
            // update indicators
            for (int i = 0; i < DCrows; i++)
            {
                int channelL = rowColumnToChannel[i, 0];
                int channelR = rowColumnToChannel[i, 1];
                DCindicators[0, i].Text = String.Format("{0:F2}", -DX / 2 + DCvalues[i] - DCvaluesDx[i] + DCvaluesLeft[i] + QuadrupoleTilt.Value);
                DCindicators[1, i].Text = String.Format("{0:F2}", +DX / 2 + DCvalues[i] + DCvaluesDx[i] + DCvaluesRight[i] - QuadrupoleTilt.Value);
            }
            label25.Text = String.Format("{0:F2}", outerL);
            label29.Text = String.Format("{0:F2}", outerR);
            label26.Text = String.Format("{0:F2}", innerL);
            label28.Text = String.Format("{0:F2}", innerR);
            label27.Text = String.Format("{0:F2}", innerC);
        }

        private void RepumperSliderOutHelper()
        {
            //Repumper HV amp input saturates at 7V
            /*if (RepumperSlider.Value > 7)
            {
                RepumperSlider.Value = 7;
            }
            if (RepumperSlider.Value < 0)
            {
                RepumperSlider.Value = 0;
            }
            Dev4AO1.OutputAnalogValue(7 - RepumperSlider.Value);*/ //changed Oct11, 2012, 935 color controlled by VCO
            Dev4AO1.OutputAnalogValue(RepumperSlider.Value);
            double output = RepumperCalib * RepumperSlider.Value;
            RepumperFrequency.Text = output.ToString("F2");
        }

        private void showZPotential(double[] voltages, int npoints)
        {
            double[] xdata = new double[npoints];
            double[] ydata = new double[npoints];
            //double[,] output = new double[2,npoints];
            double zpos;

            for (int i = 0; i < npoints; i++)
            {
                zpos = -traplength / 2 + i * traplength / npoints;
                xdata[i] = zpos;
                ydata[i] = calculatedPotential(voltages, zpos);
                //output[1,i] = xdata[i];
                //output[2,i] = ydata[i];
            }
            this.scatterPlot1.PlotXY(ydata, xdata);
            //return output;  
        }

        private double calculatedPotential(double[] v, double zpos)
        {
            double output;
            output = 0;
            for (int i = 0; i < DCrows; i++)
            {
                output += v[i] * singleElectrodePotential(electrodeZ[i] - zpos, electrodeWz[i], xdisp, wx, ytrap);
            }
            return output;
        }

        private double singleElectrodePotential(double z, double wz, double x, double wx, double y)
        {
            double output;
            double part1 = 1 / (2 * Math.PI) * Math.Atan((wx - 2 * x) * (wz - 2 * z) / (2 * y * Math.Sqrt(Math.Pow((wx - 2 * x), 2) + Math.Pow((wz - 2 * z), 2) + Math.Pow((2 * y), 2))));
            double part2 = 1 / (2 * Math.PI) * Math.Atan((wx + 2 * x) * (wz - 2 * z) / (2 * y * Math.Sqrt(Math.Pow((wx + 2 * x), 2) + Math.Pow((wz - 2 * z), 2) + Math.Pow((2 * y), 2))));
            double part3 = 1 / (2 * Math.PI) * Math.Atan((wx - 2 * x) * (wz + 2 * z) / (2 * y * Math.Sqrt(Math.Pow((wx - 2 * x), 2) + Math.Pow((wz + 2 * z), 2) + Math.Pow((2 * y), 2))));
            double part4 = 1 / (2 * Math.PI) * Math.Atan((wx + 2 * x) * (wz + 2 * z) / (2 * y * Math.Sqrt(Math.Pow((wx + 2 * x), 2) + Math.Pow((wz + 2 * z), 2) + Math.Pow((2 * y), 2))));
            output = part1 + part2 + part3 + part4;
            return output;
        }

        private void ReadConfigurationFile(string filename)
        {
            try
            {
                using (StreamReader sr = new StreamReader(filename))
                {
                    sr.ReadLine();
                    for (int i = 0; i < DCrows; i++)
                        DCsliders[i].Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    DXSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    ArrayTotalSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    DCVertDipoleSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    DCVertQuadSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    TotalBiasSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    TrapHeightSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    QuadrupoleTilt.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    QuadTiltRatioSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    TransferCavity.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    SnakeRatioSlider.Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    for (int i = 0; i < DCrows; i++)
                        DCslidersDx[i].Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    for (int i = 0; i < DCrows; i++)
                        DCslidersLeft[i].Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    for (int i = 0; i < DCrows; i++)
                        DCslidersRight[i].Value = double.Parse(sr.ReadLine().Split('\t')[1]);
                    CurrentFeedforward370Offset.Value = TransferCavity.Value;
                }
            }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SaveConfigurationFile(string filename)
        {
            try
            {
                System.IO.StreamWriter tw = new System.IO.StreamWriter(filename);

                tw.WriteLine(DateTime.Now);

                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC" + i + "\t" + DCsliders[i].Value);
                tw.WriteLine("DX tot" + "\t" + DXSlider.Value);
                tw.WriteLine("Array tot" + "\t" + ArrayTotalSlider.Value);
                tw.WriteLine("DC vert dipole" + "\t" + DCVertDipoleSlider.Value);
                tw.WriteLine("DC quad" + "\t" + DCVertQuadSlider.Value);
                tw.WriteLine("Bias tot" + "\t" + TotalBiasSlider.Value);
                tw.WriteLine("Trap Height" + "\t" + TrapHeightSlider.Value);
                tw.WriteLine("Quadrupole Tilt" + "\t" + QuadrupoleTilt.Value);
                tw.WriteLine("Quad Tilt Ratio" + "\t" + QuadTiltRatioSlider.Value);
                tw.WriteLine("Transfer Cavity" + "\t" + TransferCavity.Value);
                tw.WriteLine("Snake Inner Ratio" + "\t" + SnakeRatioSlider.Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC dx" + i + "\t" + DCslidersDx[i].Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC left" + i + "\t" + DCslidersLeft[i].Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC right" + i + "\t" + DCslidersRight[i].Value);

                tw.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //Function that saves the value of all sliders and text boxes in the VI
        private void SaveConfigurationFull(string path)
        {
            try
            {
                System.IO.StreamWriter tw = new System.IO.StreamWriter(path + DateTime.Now.ToString("HHmmss") + ".txt");

                tw.WriteLine(DateTime.Now);

                //In order of appearance, top left, to bottom right

                //Control Tab
                tw.WriteLine("ArrayTotalSlider" + "\t" + ArrayTotalSlider.Value);
                tw.WriteLine("TotalBiasSlider" + "\t" + TotalBiasSlider.Value);
                tw.WriteLine("DCVertQuadSlider" + "\t" + DCVertQuadSlider.Value);
                tw.WriteLine("DXSlider" + "\t" + DXSlider.Value);
                tw.WriteLine("QuadrupoleTilt" + "\t" + QuadrupoleTilt.Value);
                tw.WriteLine("QuadTiltRatioSlider" + "\t" + QuadTiltRatioSlider.Value);
                tw.WriteLine("SnakeRatioSlider" + "\t" + SnakeRatioSlider.Value);
                tw.WriteLine("TrapHeightSlider" + "\t" + TrapHeightSlider.Value);
                tw.WriteLine("TransferCavity" + "\t" + TransferCavity.Value);
                tw.WriteLine("CurrentFeedforward370Offset" + "\t" + CurrentFeedforward370Offset.Value);
                tw.WriteLine("CurrentFeedforward370Gain" + "\t" + CurrentFeedforward370Gain.Value);
                tw.WriteLine("SideBeam370Power" + "\t" + SideBeam370Power.Value);
                tw.WriteLine("CavityCoolingPowerControl" + "\t" + CavityCoolingPowerControl.Value);
                tw.WriteLine("RamanSlider" + "\t" + RamanSlider.Value);
                tw.WriteLine("LatticePowerControl" + "\t" + LatticePowerControl.Value);
                tw.WriteLine("RepumperSlider" + "\t" + RepumperSlider.Value);
                tw.WriteLine("ReadConfigurationFileTextbox" + "\t" + ReadConfigurationFileTextbox.Text);
                
                //Coupled DC Tab
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC" + "\t" + i + "\t" + DCsliders[i].Value);

                tw.WriteLine("LeftFingersSlider" + "\t" + LeftFingersSlider.Value);
                tw.WriteLine("SnakeOnlySlider" + "\t" + SnakeOnlySlider.Value);
                tw.WriteLine("RightFingersSlider" + "\t" + RightFingersSlider.Value);

                //Individual DC tab
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC dx" + "\t" + i + "\t" + DCslidersDx[i].Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC left" + "\t" + i + "\t" + DCslidersLeft[i].Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC right" + "\t" + i + "\t" + DCslidersRight[i].Value);

                //Electrode Scan Tab
                tw.WriteLine("ElectrodeScanDC1TextBox" + "\t" + ElectrodeScanDC1TextBox.Text);
                tw.WriteLine("ElectrodeScanDC2TextBox" + "\t" + ElectrodeScanDC2TextBox.Text);
                tw.WriteLine("ElectrodeScanStartValue1Textbox" + "\t" + ElectrodeScanStartValue1Textbox.Text);
                tw.WriteLine("ElectrodeScanEndValue1Textbox" + "\t" + ElectrodeScanEndValue1Textbox.Text);
                tw.WriteLine("ElectrodeScanStartValue2Textbox" + "\t" + ElectrodeScanStartValue2Textbox.Text);
                tw.WriteLine("ElectrodeScanEndValue2Textbox" + "\t" + ElectrodeScanEndValue2Textbox.Text);
                tw.WriteLine("ElectrodeScanPMTAveragingTextbox" + "\t" + ElectrodeScanPMTAveragingTextbox.Text);
                tw.WriteLine("ElectrodeScanNumPointsTextbox" + "\t" + ElectrodeScanNumPointsTextbox.Text);

                //Slider Scan Tab
                tw.WriteLine("SliderScanStartValueTextbox" + "\t" + SliderScanStartValueTextbox.Text);
                tw.WriteLine("SliderScanEndValueTextbox" + "\t" + SliderScanEndValueTextbox.Text);
                tw.WriteLine("SliderScanNumPointsTextbox" + "\t" + SliderScanNumPointsTextbox.Text);
                tw.WriteLine("SliderScanPMTAveragingTextbox" + "\t" + SliderScanPMTAveragingTextbox.Text);

                //Cavity Scan Tab
                tw.WriteLine("Sideband402Control" + "\t" + Sideband402Control.Value);

                //Bfield Scan Tab
                tw.WriteLine("BxSlider" + "\t" + BxSlider.Value);

                //Tickle Spectrum Tab
                tw.WriteLine("TickleSlider" + "\t" + TickleSlider.Value);

                //Correlator Tab
                tw.WriteLine("correlatorIntTimetext1" + "\t" + correlatorIntTimetext1.Text);
                tw.WriteLine("correlatorIntTimetext2" + "\t" + correlatorIntTimetext2.Text);
                tw.WriteLine("LockInFreqtext1" + "\t" + LockInFreqtext1.Text);
                tw.WriteLine("LockInFreqtext2" + "\t" + LockInFreqtext2.Text);
                tw.WriteLine("LockInFreqtext2B" + "\t" + LockInFreqtext2B.Text);
                tw.WriteLine("ArrayResetDelayText" + "\t" + ArrayResetDelayText.Text);
                tw.WriteLine("TickleResetDelayText" + "\t" + TickleResetDelayText.Text);
                tw.WriteLine("correlatorBitFilePath" + "\t" + correlatorBitFilePath.Text);
                tw.WriteLine("correlatorBitFilePathB" + "\t" + correlatorBitFilePathB.Text);
                tw.WriteLine("correlatorQtext" + "\t" + correlatorQtext.Text);
                tw.WriteLine("correlatorDiv1Ntext" + "\t" + correlatorDiv1Ntext.Text);
                tw.WriteLine("correlatorDiv2Ntext" + "\t" + correlatorDiv2Ntext.Text);
                tw.WriteLine("DataFilenameFolderPathCorr" + "\t" + DataFilenameFolderPathCorr.Text);
                tw.WriteLine("DataFilenameCommonRoot1Corr" + "\t" + DataFilenameCommonRoot1Corr.Text);

                //Pulse Programmer Tab
                tw.WriteLine("pulsePeriodText" + "\t" + pulsePeriodText.Text);
                tw.WriteLine("out1SigName" + "\t" + out1SigName.Text);
                tw.WriteLine("out1OnTimeText" + "\t" + out1OnTimeText.Text);
                tw.WriteLine("out1DelayText" + "\t" + out1DelayText.Text);
                tw.WriteLine("out2SigName" + "\t" + out2SigName.Text);
                tw.WriteLine("out2OnTimeText" + "\t" + out2OnTimeText.Text);
                tw.WriteLine("out2DelayText" + "\t" + out2DelayText.Text);
                tw.WriteLine("in1SigName" + "\t" + in1SigName.Text);
                tw.WriteLine("in1OnTimeText" + "\t" + in1OnTimeText.Text);
                tw.WriteLine("in1DelayText" + "\t" + in1DelayText.Text);
                tw.WriteLine("in2SigName" + "\t" + in2SigName.Text);
                tw.WriteLine("in2OnTimeText" + "\t" + in2OnTimeText.Text);
                tw.WriteLine("in2DelayText" + "\t" + in2DelayText.Text);

                //Camera Tab
                tw.WriteLine("CameraHbin" + "\t" + CameraHbin.Text);
                tw.WriteLine("CameraVbin" + "\t" + CameraVbin.Text);
                tw.WriteLine("CameraHstart" + "\t" + CameraHstart.Text);
                tw.WriteLine("CameraHend" + "\t" + CameraHend.Text);
                tw.WriteLine("CameraVstart" + "\t" + CameraVstart.Text);
                tw.WriteLine("CameraVend" + "\t" + CameraVend.Text);
                tw.WriteLine("CameraExposure" + "\t" + CameraExposure.Text);
                tw.WriteLine("CameraEMGain" + "\t" + CameraEMGain.Text);

                //Data Filename Tab
                tw.Write("DataFilenameChecklist" + "\t");
                for (int i = 0; i < DataFilenameChecklist.CheckedIndices.Count; i++) tw.Write(DataFilenameChecklist.CheckedIndices[i].ToString() + "\t");
                if (DataFilenameChecklist.CheckedIndices.Count > 0) tw.WriteLine(DataFilenameChecklist.CheckedIndices[DataFilenameChecklist.CheckedIndices.Count-1].ToString());
                else tw.WriteLine();
                tw.WriteLine("DetuningTextbox" + "\t" + DetuningTextbox.Text);
                tw.WriteLine("S1PowerTextbox" + "\t" + S1PowerTextbox.Text);
                tw.WriteLine("S2PowerTextbox" + "\t" + S2PowerTextbox.Text);
                tw.WriteLine("PiPowerTextbox" + "\t" + PiPowerTextbox.Text);
                tw.WriteLine("Doppler35Textbox" + "\t" + Doppler35Textbox.Text);
                tw.WriteLine("CavityPowerTextbox" + "\t" + CavityPowerTextbox.Text);
                tw.WriteLine("BxTextbox" + "\t" + BxTextbox.Text);
                tw.WriteLine("ByTextbox" + "\t" + ByTextbox.Text);
                tw.WriteLine("BzTextbox" + "\t" + BzTextbox.Text);
                tw.WriteLine("LatticeDepthTextbox" + "\t" + LatticeDepthTextbox.Text);
                tw.WriteLine("DataFilenameFolderPath" + "\t" + DataFilenameFolderPath.Text);
                tw.WriteLine("DataFilenameCommonRoot1" + "\t" + DataFilenameCommonRoot1.Text);
                tw.WriteLine("DataFilenameCommonRoot2" + "\t" + DataFilenameCommonRoot2.Text);
                
                tw.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ReadConfigurationFull(string filename)
        {
            try
            {
                using (StreamReader sr = new StreamReader(filename))
                {
                    char[] charSeparators = new char[] { '\t' };

                    while (!sr.EndOfStream)
                    {
                        String theString = sr.ReadLine();
                        switch (theString.Split('\t')[0])
                        {
                            case "ArrayTotalSlider":
                                ArrayTotalSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "TotalBiasSlider":
                                TotalBiasSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "DCVertQuadSlider":
                                DCVertQuadSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "DXSlider":
                                DXSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "QuadrupoleTilt":
                                QuadrupoleTilt.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "QuadTiltRatioSlider":
                                QuadTiltRatioSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "SnakeRatioSlider":
                                SnakeRatioSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "TrapHeightSlider":
                                TrapHeightSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "TransferCavity":
                                TransferCavity.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "CurrentFeedforward370Offset":
                                CurrentFeedforward370Offset.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "CurrentFeedforward370Gain":
                                CurrentFeedforward370Gain.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "SideBeam370Power":
                                SideBeam370Power.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "CavityCoolingPowerControl":
                                CavityCoolingPowerControl.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "RamanSlider":
                                RamanSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "RepumperSlider":
                                RepumperSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "ReadConfigurationFileTextbox":
                                ReadConfigurationFileTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "DC":
                                DCsliders[0].Value = double.Parse(theString.Split('\t')[2]);
                                for (int i = 1; i < DCrows; i++)
                                    DCsliders[i].Value = double.Parse(sr.ReadLine().Split('\t')[2]);
                                break;
                            case "LeftFingersSlider":
                                LeftFingersSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "SnakeOnlySlider":
                                SnakeOnlySlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "RightFingersSlider":
                                RightFingersSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "DC dx":
                                DCslidersLeft[0].Value = double.Parse(theString.Split('\t')[2]);
                                for (int i = 1; i < DCrows; i++)
                                    DCslidersLeft[i].Value = double.Parse(sr.ReadLine().Split('\t')[2]);
                                break;
                            case "DC left":
                                DCslidersDx[0].Value = double.Parse(theString.Split('\t')[2]);
                                for (int i = 1; i < DCrows; i++)
                                    DCslidersDx[i].Value = double.Parse(sr.ReadLine().Split('\t')[2]);
                                break;
                            case "DC right":
                                DCslidersRight[0].Value = double.Parse(theString.Split('\t')[2]);
                                for (int i = 1; i < DCrows; i++)
                                    DCslidersRight[i].Value = double.Parse(sr.ReadLine().Split('\t')[2]);
                                break;
                            case "ElectrodeScanDC1TextBox":
                                ElectrodeScanDC1TextBox.Text = theString.Split('\t')[1];
                                break;
                            case "ElectrodeScanDC2TextBox":
                                ElectrodeScanDC2TextBox.Text = theString.Split('\t')[1];
                                break;
                            case "ElectrodeScanStartValue1Textbox":
                                ElectrodeScanStartValue1Textbox.Text = theString.Split('\t')[1];
                                break;
                            case "ElectrodeScanEndValue1Textbox":
                                ElectrodeScanEndValue1Textbox.Text = theString.Split('\t')[1];
                                break;
                            case "ElectrodeScanStartValue2Textbox":
                                ElectrodeScanStartValue2Textbox.Text = theString.Split('\t')[1];
                                break;
                            case "ElectrodeScanEndValue2Textbox":
                                ElectrodeScanEndValue2Textbox.Text = theString.Split('\t')[1];
                                break;
                            case "ElectrodeScanPMTAveragingTextbox":
                                ElectrodeScanPMTAveragingTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "ElectrodeScanNumPointsTextbox":
                                ElectrodeScanStartValue2Textbox.Text = theString.Split('\t')[1];
                                break;
                            case "Sideband402Control":
                                Sideband402Control.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "SliderScanStartValueTextbox":
                                SliderScanStartValueTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "SliderScanEndValueTextbox":
                                SliderScanEndValueTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "SliderScanNumPointsTextbox":
                                SliderScanNumPointsTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "SliderScanPMTAveragingTextbox":
                                SliderScanPMTAveragingTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "BxSlider":
                                BxSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "TickleSlider":
                                TickleSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "correlatorIntTimetext1":
                                correlatorIntTimetext1.Text = theString.Split('\t')[1];
                                break;
                            case "correlatorIntTimetext2":
                                correlatorIntTimetext2.Text = theString.Split('\t')[1];
                                break;
                            case "LockInFreqtext1":
                                LockInFreqtext1.Text = theString.Split('\t')[1];
                                break;
                            case "LockInFreqtext2":
                                LockInFreqtext2.Text = theString.Split('\t')[1];
                                break;
                            case "LockInFreqtext2B":
                                LockInFreqtext2B.Text = theString.Split('\t')[1];
                                break;
                            case "ArrayResetDelayText":
                                ArrayResetDelayText.Text = theString.Split('\t')[1];
                                break;
                            case "TickleResetDelayText":
                                TickleResetDelayText.Text = theString.Split('\t')[1];
                                break;
                            case "correlatorBitFilePath":
                                correlatorBitFilePath.Text = theString.Split('\t')[1];
                                break;
                            case "correlatorBitFilePathB":
                                correlatorBitFilePathB.Text = theString.Split('\t')[1];
                                break;
                            case "correlatorQtext":
                                correlatorQtext.Text = theString.Split('\t')[1];
                                break;
                            case "correlatorDiv1Ntext":
                                correlatorDiv1Ntext.Text = theString.Split('\t')[1];
                                break;
                            case "correlatorDiv2Ntext":
                                correlatorDiv2Ntext.Text = theString.Split('\t')[1];
                                break;
                            case "DataFilenameFolderPathCorr":
                                DataFilenameFolderPathCorr.Text = theString.Split('\t')[1];
                                break;
                            case "DataFilenameCommonRoot1Corr":
                                DataFilenameCommonRoot1Corr.Text = theString.Split('\t')[1];
                                break;
                            case "pulsePeriodText":
                                pulsePeriodText.Text = theString.Split('\t')[1];
                                break;
                            case "out1SigName":
                                out1SigName.Text = theString.Split('\t')[1];
                                break;
                            case "out1OnTimeText":
                                out1OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "out1DelayText":
                                out1DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "out2SigName":
                                out2SigName.Text = theString.Split('\t')[1];
                                break;
                            case "out2OnTimeText":
                                out2OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "out2DelayText":
                                out2DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "in1SigName":
                                in1SigName.Text = theString.Split('\t')[1];
                                break;
                            case "in1OnTimeText":
                                in1OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "in1DelayText":
                                in1DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "in2SigName":
                                in2SigName.Text = theString.Split('\t')[1];
                                break;
                            case "in2OnTimeText":
                                in2OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "in2DelayText":
                                in2DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "CameraHbin":
                                CameraHbin.Text = theString.Split('\t')[1];
                                break;
                            case "CameraVbin":
                                CameraVbin.Text = theString.Split('\t')[1];
                                break;
                            case "CameraHstart":
                                CameraHstart.Text = theString.Split('\t')[1];
                                break;
                            case "CameraHend":
                                CameraHend.Text = theString.Split('\t')[1];
                                break;
                            case "CameraVstart":
                                CameraVstart.Text = theString.Split('\t')[1];
                                break;
                            case "CameraVend":
                                CameraVend.Text = theString.Split('\t')[1];
                                break;
                            case "CameraExposure":
                                CameraExposure.Text = theString.Split('\t')[1];
                                break;
                            case "CameraEMGain":
                                CameraEMGain.Text = theString.Split('\t')[1];
                                break;
                            case "DataFilenameChecklist":
                                for (int i = 1; i < theString.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries).GetLength(0); i++)
                                {
                                    DataFilenameChecklist.SetItemCheckState(int.Parse(theString.Split('\t')[i]),CheckState.Checked);
                                }
                                break;
                            case "DetuningTextbox":
                                DetuningTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "S1PowerTextbox":
                                S1PowerTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "S2PowerTextbox":
                                S2PowerTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "PiPowerTextbox":
                                PiPowerTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "Doppler35Textbox":
                                Doppler35Textbox.Text = theString.Split('\t')[1];
                                break;
                            case "CavityPowerTextbox":
                                CavityPowerTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "BxTextbox":
                                BxTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "ByTextbox":
                                ByTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "BzTextbox":
                                BzTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "LatticeDepthTextbox":
                                LatticeDepthTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "DataFilenameFolderPath":
                                DataFilenameFolderPath.Text = theString.Split('\t')[1];
                                break;
                            case "DataFilenameCommonRoot1":
                                DataFilenameCommonRoot1.Text = theString.Split('\t')[1];
                                break;
                            case "DataFilenameCommonRoot2":
                                DataFilenameCommonRoot2.Text = theString.Split('\t')[1];
                                break;
                        }
                    }
                }
            }
            catch (System.IO.FileNotFoundException ex){MessageBox.Show(ex.Message);}
        }

        //VCOVVAoutputHelper
        //Function that takes as an input a frequency and produces a voltage
        //for the VCO and VVA based on calibration of the 402 sideband VCO + VVA
        private void VCOVVAoutputHelper(double frequency)
        {
            //compute voltages base on calibration
            double VCOout = 8.3294 - 0.032034 * frequency + 0.0001208 * Math.Pow(frequency, 2) - 3.2046 * Math.Pow(10, -7) * Math.Pow(frequency, 3) + 2.858 * Math.Pow(10, -10) * Math.Pow(frequency, 4);
            double VVAout = 8.6846 - 24.254 * VCOout + 33.647 * Math.Pow(VCOout, 2) - 23.429 * Math.Pow(VCOout, 3) + 7.6238 * Math.Pow(VCOout, 4) - 0.3127 * Math.Pow(VCOout, 5) - 0.54357 * Math.Pow(VCOout, 6) + 0.16718 * Math.Pow(VCOout, 7) - 0.020405 * Math.Pow(VCOout, 8) + 0.00093025 * Math.Pow(VCOout, 9);
            //output to DAC
            Dev4AO6.OutputAnalogValue(VCOout);
            Dev4AO7.OutputAnalogValue(VVAout);
        }

        //Smooth402SidebandTuning
        //Tunes the 402 sideband smoothly from current frequency to start freq for scan
        private void Smooth402SidebandTuning(double FreqCurrent, double FreqFinal)
        {
            double df = 0.5;
            double freq = FreqCurrent;
            if (FreqFinal < freq)
            {
                while (freq > FreqFinal)
                {
                    freq = freq - df;
                    VCOVVAoutputHelper(freq);
                    Thread.Sleep(10);
                }
            }
            if (FreqFinal > freq)
            {
                while (freq < FreqFinal)
                {
                    freq = freq + df;
                    VCOVVAoutputHelper(freq);
                    Thread.Sleep(10);
                }
            }
        }

        private void RampArrayAux()
        {
            DAC.Clear();
            double T = 0.5; int samples = 200;
            double cameraExposure = 0.150;
            double blankWaitTime = 0.5;
            DAC.Mask = 0;
            const double rampAmplitude = 2;
            double[] rampInitVal = new double[] { innerL, innerR, innerC };
            double[] rampFinalVal = new double[] { innerL - SnakeRatioSlider.Value * rampAmplitude, innerR - SnakeRatioSlider.Value * rampAmplitude, innerC - rampAmplitude };
            DAC.Ramp(new int[] { (int)this.RFelectrodeToChannel["innerL"], (int)this.RFelectrodeToChannel["innerR"], (int)this.RFelectrodeToChannel["snake"] }, rampInitVal, rampFinalVal, ref T, ref samples);
            DAC.Wait(ref blankWaitTime);
            DAC.Mask = 128;
            DAC.Wait(ref cameraExposure);
            DAC.Mask = 0;
            DAC.Wait(ref blankWaitTime);
            DAC.Ramp(new int[] { (int)this.RFelectrodeToChannel["innerL"], (int)this.RFelectrodeToChannel["innerR"], (int)this.RFelectrodeToChannel["snake"] }, rampFinalVal, rampInitVal, ref T, ref samples);
            DAC.Wait(ref blankWaitTime);
            DAC.Mask = 128;
            DAC.Wait(ref cameraExposure);
            DAC.Mask = 0;
            double cameraReset = .150;
            DAC.Wait(ref cameraReset);
            DAC.Commit();
        }

        //
        // Method to obtain filename from "Data Filename Control"
        //
        public string[] GetDataFilename(int what)
        {
            string[] theString = new string[2];
            //path + root

            if (what == 1)
            {
                theString[0] = DataFilenameFolderPath.Text;
                if (CommonFilenameSwitch.Value) { theString[1] = DataFilenameCommonRoot1.Text + " "; }
                else { theString[1] = DataFilenameCommonRoot2.Text + " "; }
            }
            else if (what == 2)
            {
                theString[0] = DataFilenameFolderPathCorr.Text;
                theString[1] = DataFilenameCommonRoot1Corr.Text + " ";
            }

            for (int i = 0; i < DataFilenameChecklist.CheckedIndices.Count; i++)
            {
                switch (DataFilenameChecklist.CheckedIndices[i])
                {
                    case 0:
                        theString[1] += "camdt=" + CameraExposure.Text + "s ";
                        break;
                    case 1:
                        theString[1] += "EM=" + CameraEMGain.Text + " ";
                        break;
                    case 2:
                        if (intTselector.Value) { theString[1] += "corrdt=" + correlatorIntTimetext1.Text + "ms "; }
                        else { theString[1] += "corrdt=" + correlatorIntTimetext2.Text + "ms "; }
                        break;
                    case 3:
                        theString[1] += "ppdc=" + RegRecPerText.Text + " ";
                        break;
                    case 4:
                        theString[1] += "ppdf=" + ncorrbinsText.Text + "kHz ";
                        break;
                    case 5:
                        theString[1] += "det=" + DetuningTextbox.Text + " ";
                        break;
                    case 6:
                        theString[1] += "sig1=" + S1PowerTextbox.Text + " ";
                        break;
                    case 7:
                        theString[1] += "sig2=" + S2PowerTextbox.Text + " ";
                        break;
                    case 8:
                        theString[1] += "pi=" + PiPowerTextbox.Text + " ";
                        break;
                    case 9:
                        theString[1] += "dop35=" + Doppler35Textbox.Text + " ";
                        break;
                    case 10:
                        theString[1] += "cav=" + CavityPowerTextbox.Text + " ";
                        break;
                    case 11:
                        theString[1] += "Bx=" + BxTextbox.Text + " ";
                        break;
                    case 12:
                        theString[1] += "By=" + ByTextbox.Text + " ";
                        break;
                    case 13:
                        theString[1] += "Bz=" + BzTextbox.Text + " ";
                        break;
                    case 14:
                        theString[1] += "Ulatt=" + LatticeDepthTextbox.Text + " ";
                        break;
                    case 15:
                        theString[1] += "array=" + ArrayTotalSlider.Value.ToString("F2") + " ";
                        break;
                    case 16:
                        theString[1] += "DY=" + TotalBiasSlider.Value.ToString("F2") + " ";
                        break;
                    case 17:
                        theString[1] += "DCQuad=" + DCVertQuadSlider.Value.ToString("F2") + " ";
                        break;
                    case 18:
                        theString[1] += "DX=" + DXSlider.Value.ToString("F3") + " ";
                        break;
                    case 19:
                        theString[1] += "QuadTilt=" + QuadrupoleTilt.Value.ToString("F2") + " ";
                        break;
                    case 20:
                        theString[1] += "QuadRatio=" + QuadTiltRatioSlider.Value.ToString("F2") + " ";
                        break;
                    case 21:
                        theString[1] += "ArrayRatio=" + SnakeRatioSlider.Value.ToString("F2") + " ";
                        break;

                }
            }
            return theString;
        }

        //Method to save scan data
        private void SaveScanData(ThreadHelperClass threadHelper)
        {
            try
            {
                //get filename from control parameters tab
                string[] filename = GetDataFilename(1);

                System.IO.StreamWriter tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " Settings " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                tw.WriteLine("Electrodes Settings");

                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC" + i + "\t" + DCsliders[i].Value);
                tw.WriteLine("DX tot" + "\t" + DXSlider.Value);
                tw.WriteLine("Array tot" + "\t" + ArrayTotalSlider.Value);
                tw.WriteLine("DC vert dipole" + "\t" + DCVertDipoleSlider.Value);
                tw.WriteLine("DC quad" + "\t" + DCVertQuadSlider.Value);
                tw.WriteLine("Bias tot" + "\t" + TotalBiasSlider.Value);
                tw.WriteLine("Trap Height" + "\t" + TrapHeightSlider.Value);
                tw.WriteLine("Quadrupole Tilt" + "\t" + QuadrupoleTilt.Value);
                tw.WriteLine("Quad Tilt Ratio" + "\t" + QuadTiltRatioSlider.Value);
                tw.WriteLine("Transfer Cavity" + "\t" + TransferCavity.Value);
                tw.WriteLine("Snake Inner Ratio" + "\t" + SnakeRatioSlider.Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC dx" + i + "\t" + DCslidersDx[i].Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC left" + i + "\t" + DCslidersLeft[i].Value);
                for (int i = 0; i < DCrows; i++)
                    tw.WriteLine("DC right" + i + "\t" + DCslidersRight[i].Value);
                tw.Close();


                if (threadHelper.message == "PMT" || threadHelper.message == "PMT & Camera")
                {
                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " PMT Data " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                    for (int i = 0; i < threadHelper.numPoints; i++)
                        tw.WriteLine(threadHelper.DoubleScanVariable[0, i] + "\t" + threadHelper.DoubleData[0, i] + "\t" + threadHelper.DoubleData[1, i]);

                    tw.Close();
                }

                if (threadHelper.message == "Dev3AI2")
                {
                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " AI Data " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                    for (int i = 0; i < threadHelper.numPoints; i++)
                        tw.WriteLine(threadHelper.DoubleScanVariable[0, i] + "\t" + threadHelper.DoubleData[0, i]);

                    tw.Close();
                }

                if (threadHelper.message == "Correlator:Sum")
                {
                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " CorrelatorSum Data " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                    for (int i = 0; i < threadHelper.numPoints; i++)
                        tw.WriteLine(threadHelper.DoubleScanVariable[0, i] + "\t" + threadHelper.DoubleData[0, i]);

                    tw.Close();
                }

                if (threadHelper.message == "Camera" || threadHelper.message == "PMT & Camera")
                {
                    //Save fluorescence data
                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " Fluorescence Log " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                    //Write scan variable first
                    for (int j = 0; j < threadHelper.numPoints - 1; j++)
                    {
                        tw.Write(threadHelper.DoubleScanVariable[0, j] + ",");
                    }
                    tw.WriteLine(threadHelper.DoubleScanVariable[0, threadHelper.numPoints - 1]);

                    int NumPlot = CameraForm.FluorescenceGraph.Plots.Count;
                    double[] data;
                    for (int i = 0; i < NumPlot; i++)
                    {
                        data = CameraForm.FluorescenceGraph.Plots[i].GetYData();
                        for (int j = 0; j < data.Length - 1; j++)
                        {
                            tw.Write(data[j] + ",");
                        }
                        tw.WriteLine(data[data.Length - 1]);
                    }

                    tw.Close();

                    //Save position data

                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " Position Log " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                    //Write scan variable first
                    for (int j = 0; j < threadHelper.numPoints - 1; j++)
                    {
                        tw.Write(threadHelper.DoubleScanVariable[0, j] + ",");
                    }
                    tw.WriteLine(threadHelper.DoubleScanVariable[0, threadHelper.numPoints - 1]);

                    NumPlot = CameraForm.PositionGraph.Plots.Count;
                    for (int i = 0; i < NumPlot; i++)
                    {
                        data = CameraForm.PositionGraph.Plots[i].GetYData();
                        for (int j = 0; j < data.Length - 1; j++)
                        {
                            tw.Write(data[j] + ",");
                        }
                        tw.WriteLine(data[data.Length - 1]);
                    }

                    tw.Close();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //
        // FORM EVENTS, BUTTON CLICKS ETC.
        //

        private void CorrelatorBinningPhaseSliderOut(object sender, EventArgs e)
        {
            theCorrelator.binningPhase = CorrelatorBinningPhaseSlider.Value;
        }
        private void RamanSliderOut(object sender, EventArgs e)
        {
            Dev4AO0.OutputAnalogValue((double)RamanSlider.Value);
        }
        private void BxSliderOut(object sender, EventArgs e)
        {
            BxSliderOutHelper();
        }
        private void TickleSliderOut(object sender, EventArgs e)
        {
            TickleSliderOutHelper();
        }
        private void BxSliderOutHelper()
        {
            Dev7AO2.OutputAnalogValue((double)BxSlider.Value / BxCalib);
        }
        private void TickleSliderOutHelper()
        {
            Dev7AO0.OutputAnalogValue((double)TickleSlider.Value / TickleCalib);
        }
        private void RepumperPowerSliderOutHelper()
        {
            Dev4AO4.OutputAnalogValue((double)RepumperPowerSlider.Value);
        }
        private void Sideband402ControlOut(object sender, EventArgs e)
        {
            VCOVVAoutputHelper((double)Sideband402Control.Value);
        }
        private void RepumperPowerSliderOut(object sender, EventArgs e)
        {
            RepumperPowerSliderOutHelper();
        }
        private void SideBeam370PowerOut(object sender, EventArgs e)
        {
            Dev4AO5.OutputAnalogValue((double)SideBeam370Power.Value);
        }
        private void LatticePowerControlOut(object sender, EventArgs e)
        {
            Dev7AO6.OutputAnalogValue((double)LatticePowerControl.Value);
        }
        private void APDBiasOut(object sender, EventArgs e)
        {
            Dev7AO7.OutputAnalogValue((double)CavityCoolingPowerControl.Value);
        }
        private void TransferCavityOut(object sender, EventArgs e)
        {
            Dev4AO3.OutputAnalogValue((double)TransferCavity.Value / TCcalib); 
        }

        private void RepumperSliderOut(object sender, EventArgs e)
        {
            RepumperSliderOutHelper();
        }

        private void ReadElectrodeConfig_Click_1(object sender, EventArgs e)
        {
            ReadConfigurationFile(ReadConfigurationFileTextbox.Text);
            UpdateAll();
        }

        private void SaveFullConfigButton_Click(object sender, EventArgs e)
        {
            SaveConfigurationFull(SaveFullConfigTextbox.Text);
        }

        private void ReadFullConfigButton_Click(object sender, EventArgs e)
        {
            ReadConfigurationFull(ReadFullConfigTextbox.Text);
            UpdateAll();
        }

        private void IonDanceButton_Click(object sender, EventArgs e)
        {
            DAC.Clear();
            DAC.AnalogOutput(new int[] { RFelectrodeToChannel["innerL"], RFelectrodeToChannel["snake"], RFelectrodeToChannel["innerR"], this.rowToChannel[danceDCrow][0], this.rowToChannel[danceDCrow][1] }, this.danceWaveform, danceSampleRate);
            DAC.Commit();
        }

        private void RepumperSlider_Adjusted(object sender, EventArgs e)
        {
            Dev4AO1.OutputAnalogValue((double)RepumperSlider.Value);
        }
        
        private void BxSlider_Adjusted(object sender, EventArgs e)
        {
            Dev7AO2.OutputAnalogValue((double)BxSlider.Value / BxCalib);
        }

        private void TickleSlider_Adjusted(object sender, EventArgs e)
        {
            Dev7AO0.OutputAnalogValue((double)TickleSlider.Value / TickleCalib);
        }

        private void RepumperPowerSlider_Adjusted(object sender, EventArgs e)
        {
            Dev4AO4.OutputAnalogValue((double)RepumperPowerSlider.Value);
        }

        private void IonizationShutter_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            Dev2DO0.OutputDigitalValue(IonizationShutter.Value);
        }

        private void LockinFrequencySwitch_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            Dev2DO4.OutputDigitalValue(LockinFrequencySwitch.Value);
        }

        private void CavityBeam370Switch_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            Dev2DO1.OutputDigitalValue(CavityBeam370Switch.Value);
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            DAC.Reset();
        }

        private void RepumperRFBroadenSwitch_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            Dev2DO2.OutputDigitalValue(RepumperRFBroadenSwitch.Value);
        }
        private void Switch638_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            Dev2DO6.OutputDigitalValue(!Switch638.Value);
        }

        private void Dev2DO3Switch_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            Dev2DO3.OutputDigitalValue(Dev2DO3Switch.Value);
        }

        private void CameraHbin_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }
        private void CameraVbin_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }
        private void CameraExposure_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }
        private void CameraEMGain_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }
        private void CameraHstart_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }
        private void CameraHend_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }
        private void CameraVstart_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }
        private void CameraVend_LostFocus(object sender, EventArgs e)
        {
            CameraThreadHelper.flag = true;
        }

        //
        // Shut down camera when closing the application
        //

        private void Form1_Closing(object sender, EventArgs e)
        {
            //Save slider values and textbox values for the application
            SaveConfigurationFull(SaveFullConfigTextbox.Text);
            //Shutdown camera
            if (Camera != null)
            {
                Camera.AppShutDown();
            }
        }

        private void Form1_Disposed(object sender, System.EventArgs e)
        {
            //Save slider values and textbox values for the application
            SaveConfigurationFull(SaveFullConfigTextbox.Text);
        }

        ///////////////////////////
        //
        //MULTITHREADING CODE BELOW
        //
        ///////////////////////////



        private double gpibDoubleResult()
        {
            if (gpibdevice != null && gpibdevice.stringRead != null)
            {
                //get reading from GPIB object
                string result = gpibdevice.stringRead;
                //find position of first + sign
                int i = 0;
                string result2 = result.Substring(i, 1);
                while (result2[0] != '+')
                {
                    i++;
                    result2 = result.Substring(i, 1);
                }
                i++;
                //extract digits
                result2 = result.Substring(i, 7);
                //extract exponent
                string result3 = result.Substring(i, 1);
                while (result3[0] != 'E')
                {
                    i++;
                    result3 = result.Substring(i, 1);
                }
                i++;
                result3 = result.Substring(i, 3);
                //extract exponent
                double multiple = double.Parse(result3);
                //convert digits string to double
                double digits = double.Parse(result2);
                //create standard notation count
                double display = Math.Pow(10, multiple) * digits;

                return display;
            }
            else { return 0; }
        }

        //
        //SINGLE PMT COUNTS
        //

        private void SinglePMTReadExecute()
        {
            //reset device before starting
            gpibdevice.Reset();

            while (SinglePMTReadThreadHelper.ShouldBeRunningFlag)
            {
                //get reading from GPIB counter
                gpibdevice.simpleRead(21);
                //threading structure that tries to pass a message to the main thread
                try
                {
                    this.Invoke(new MyDelegate(SinglePMTReadExecuteFrmCallback));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void SinglePMTReadExecuteFrmCallback()
        {
            double display = gpibDoubleResult();
            //display count
            CameraForm.PMTcountBox.Text = display.ToString();
            //plot
            CameraForm.PMTcountGraph.PlotYAppend(display);
        }


        private void SinglePMTReadButton_Click(object sender, EventArgs e)
        {
            if (!SinglePMTReadThreadHelper.ShouldBeRunningFlag)
            {
                SinglePMTReadThreadHelper.ShouldBeRunningFlag = true;
                SinglePMTReadThreadHelper.theThread = new Thread(new ThreadStart(SinglePMTReadExecute));
                SinglePMTReadThreadHelper.theThread.Name = "Single PMT Read thread";
                SinglePMTReadThreadHelper.theThread.Priority = ThreadPriority.Normal;
                SinglePMTReadButton.BackColor = System.Drawing.Color.Green;
                //start scan thread
                try
                {
                    SinglePMTReadThreadHelper.theThread.Start();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                SinglePMTReadThreadHelper.ShouldBeRunningFlag = false;
                SinglePMTReadButton.BackColor = System.Drawing.Color.Gray;
            }
        }

        //
        //CORRELATOR
        //

        private void CorrelatorGetResultsHelper()
        {
            //Raise wire to tell FPGA to start collecting data

            //get reading from Correlator FPGA
            //Wait for Ch1 and Ch2 flags to be raised
            theCorrelator.GetResults();
            while (!(theCorrelator.feedflagCh1 && theCorrelator.feedflagCh2))
            {
                theCorrelator.GetResults();
            }
            //Lower wire to stop FPGA acquiring

            //If array reset checked, lower array to 0 and back to initial value
            if (ArrayReset.Checked)
            {
                try
                {
                    this.Invoke(new MyDelegate(CorrelatorArrayResetFormCallback));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            //If array reset checked, lower array to 0 and back to initial value
            if (TickleReset.Checked)
            {
                try
                {
                    this.Invoke(new MyDelegate(CorrelatorTickleResetFormCallback));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void CorrelatorArrayResetFormCallback()
        {
            double initialValue = ArrayTotalSlider.Value;
            //reset
            ArrayTotalSlider.Value = 0;
            //update DAC
            compensationAdjustedHelper();
            //wait for low pass
            Thread.Sleep(int.Parse(ArrayResetDelayText.Text));
            //go back
            ArrayTotalSlider.Value = initialValue;
            //update DAC
            compensationAdjustedHelper();
            //wait for low pass
            Thread.Sleep(int.Parse(ArrayResetDelayText.Text));
        }

        private void CorrelatorTickleResetFormCallback()
        {
            //turn off tickle via RF switch, (TTL high selects RFOUT2)
            Dev2DO5.OutputDigitalValue(true);
            //wait
            Thread.Sleep(int.Parse(TickleResetDelayText.Text));
            //turn on tickle again
            Dev2DO5.OutputDigitalValue(false);
        }   

        private bool CorrelatorParameterInit()
        {
            //Initialize Correlator Parameters
            //theCorrelator = new Correlator();
            if (chooseCode.Value)
            { theCorrelator.ok.P = int.Parse(correlatorPtext.Text); }
            else
            { theCorrelator.ok.P = int.Parse(correlatorPtextB.Text); };
            
            // assign the number of correlator bins
            ncorrbins = int.Parse(ncorrbinsText.Text);
            theCorrelator.lshiftreg = ncorrbins;

            theCorrelator.ok.Q = int.Parse(correlatorQtext.Text);
            theCorrelator.ok.Div1N = int.Parse(correlatorDiv1Ntext.Text);
            theCorrelator.ok.Div2N = int.Parse(correlatorDiv2Ntext.Text);
            if (intTselector.Value)
            {
                theCorrelator.IntTime = int.Parse(correlatorIntTimetext1.Text);
            }
            else
            {
                theCorrelator.IntTime = int.Parse(correlatorIntTimetext2.Text);
            }

            //if (syncSrcSw.Value)
            //{
                if (LockinFrequencySwitch.Value)
                {
                    theCorrelator.ClkDiv = (uint)(Math.Round(theCorrelator.ok.P * 1000 / ncorrbins / double.Parse(LockInFreqtext1.Text) - 1, 0));
                }
                else
                {
                    if (chooseCode.Value)
                    { theCorrelator.ClkDiv = (uint)(Math.Round(theCorrelator.ok.P * 1000 / ncorrbins / double.Parse(LockInFreqtext2.Text) - 1, 0)); }
                    else
                    { theCorrelator.ClkDiv = (uint)(Math.Round(theCorrelator.ok.P * 1000 / ncorrbins / double.Parse(LockInFreqtext2B.Text) - 1, 0)); }
                }
            //}
            //else
            //{
            //    theCorrelator.ClkDiv = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(pulsePeriodText.Text) / ncorrbins, 0));
            //}
            
            theCorrelator.PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(pulsePeriodText.Text), 0));

            theCorrelator.onTimeOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1OnTimeText.Text), 0));
            theCorrelator.onTimeOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2OnTimeText.Text), 0));
            theCorrelator.delayOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1DelayText.Text), 0));
            theCorrelator.delayOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2DelayText.Text), 0));

            theCorrelator.onTimeIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1OnTimeText.Text), 0));
            theCorrelator.onTimeIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2OnTimeText.Text), 0));
            theCorrelator.delayIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1DelayText.Text), 0));
            theCorrelator.delayIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2DelayText.Text), 0));


            theCorrelator.slow_PulseClkDiv = uint.Parse(slow_pulsePeriodText.Text);

            theCorrelator.slow_onTimeOut[0] = uint.Parse(slow_out1OnTimeText.Text);
            theCorrelator.slow_onTimeOut[1] = uint.Parse(slow_out2OnTimeText.Text);
            theCorrelator.slow_delayOut[0] = uint.Parse(slow_out1DelayText.Text);
            theCorrelator.slow_delayOut[1] = uint.Parse(slow_out2DelayText.Text);

            theCorrelator.slow_onTimeIn[0] = uint.Parse(slow_in1OnTimeText.Text);
            theCorrelator.slow_onTimeIn[1] = uint.Parse(slow_in2OnTimeText.Text);
            theCorrelator.slow_delayIn[0] = uint.Parse(slow_in1DelayText.Text);
            theCorrelator.slow_delayIn[1] = uint.Parse(slow_in2DelayText.Text);

            //theCorrelator.PulseClkDiv = (int)(Math.Round(theCorrelator.ok.P * 1000 / double.Parse(pulseFreqText.Text) - 1, 0));
            //theCorrelator.PulseWidthDiv = (int)(Math.Round(theCorrelator.PulseClkDiv * double.Parse(pulsedDutyText.Text)));

            theCorrelator.bound1 = int.Parse(correlatorBound1text.Text);
            theCorrelator.bound2 = int.Parse(correlatorBound2text.Text);

            //Set boolean in correlator for sync signal source
            if (syncSrcSw.Value) { theCorrelator.syncSrcChoose = true; }
            else { theCorrelator.syncSrcChoose = false; }
            

            //Attempt Initialize
            bool auxInitBool = true;
            if (chooseCode.Value)
            { auxInitBool = theCorrelator.Init(correlatorBitFilePath.Text); }
            else
            { auxInitBool = theCorrelator.Init(correlatorBitFilePathB.Text); }

            //return status of init
            return auxInitBool;
        }

        private void CorrelatorExecute()
        {
            //clear graph
            CameraForm.scatterGraph3.ClearData();
            //initialize parameters and attempt init, continue only if init returns true
            if (CorrelatorParameterInit())
            {
                //post ID, errors
                //post frequencies
                try
                {
                    this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                while (CorrelatorThreadHelper.ShouldBeRunningFlag)
                {
                    //get reading from Correlator FPGA
                    CorrelatorGetResultsHelper();

                    //Correlator Plots
                    try
                    {
                        this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallbackCh1));
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }

                    /*
                    if (theCorrelator.feedflagCh1)
                    {
                        //threading structure that tries to pass a message to the main thread
                        try
                        {
                            this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallbackCh1));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        //wait
                        //Thread.Sleep(int.Parse(correlatorIntTimetext.Text));
                    }

                    if (theCorrelator.feedflagCh2)
                    {
                        //threading structure that tries to pass a message to the main thread
                        try
                        {
                            this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallbackCh2));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        //wait
                        //Thread.Sleep(int.Parse(correlatorIntTimetext.Text));
                    }
                    */
                      
                }
            }

            //Thread finishes
            CorrelatorThreadHelper.ShouldBeRunningFlag = false;

            //Update button
            try
            {
                this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallback2));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            //((IDisposable)theCorrelator).Dispose();
        }


        private void CorrelatorExecuteFrmCallbackCh1()
        {

            double ctot = 0;

            // initialize x-axis array (bin indices) for plotting correlator curves as XY plots
            double[] corrbins = new double[ncorrbins];
            for (int cbin = 0; cbin < ncorrbins; cbin++)
            {
                corrbins[cbin] = cbin + 1;
            }

            // retrieve the averaged data on the plot so far:
            double[] prevCorrDataCh1 = CameraForm.scatterGraph3.Plots[0].GetYData();
            double[] prevCorrDataCh2 = CameraForm.scatterGraph3.Plots[1].GetYData();
            double[] prevCorrDataDiff = CameraForm.scatterGraph3.Plots[2].GetYData();
            double[] prevCorrDataSum = CameraForm.scatterGraph3.Plots[3].GetYData();
            // new correlator trace that came in from the FPGA:
            double[] newCorrDataCh1 = theCorrelator.phcountarrayCh1;
            double[] newCorrDataCh2 = theCorrelator.phcountarrayCh2;
            double[] newCorrDataDiff = new double[ncorrbins];
            double[] newCorrDataSum = new double[ncorrbins];
            double[] newnormSig = new double[ncorrbins];
            // initialize averaged data vectors
            double[] avgCorrDataCh1 = new double[ncorrbins];
            double[] avgCorrDataCh2 = new double[ncorrbins];
            double[] avgCorrDataDiff = new double[ncorrbins];
            double[] avgCorrDataSum = new double[ncorrbins];
            double[] avgnormSig = new double[ncorrbins];

            for (int j = 0; j < newCorrDataCh1.Length; j++)
            {
                newCorrDataDiff[j] = newCorrDataCh1[j] - newCorrDataCh2[j];
                newCorrDataSum[j] = newCorrDataCh1[j] + newCorrDataCh2[j];
                newnormSig[j] = newCorrDataDiff[j] / (newCorrDataSum[j] + 2 * (int.Parse(textBoxBackEst.Text)));
            }

            // plot the new correlator data 
            CorrelatorGraph.Plots[0].PlotY(newCorrDataCh1);
            CorrelatorGraph.Plots[1].PlotY(newCorrDataCh2);
            CorrelatorGraph.Plots[2].PlotY(newCorrDataDiff);
            CorrelatorGraph.Plots[3].PlotY(newCorrDataSum);

            // Display count RATE as a function of time
            CameraForm.PMTcountGraph.Plots[0].PlotYAppend(theCorrelator.totalCountsCh1 / theCorrelator.IntTime * 1000);
            CameraForm.PMTcountGraph.Plots[1].PlotYAppend(theCorrelator.totalCountsCh2 / theCorrelator.IntTime * 1000);
            ctot = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;
            CameraForm.PMTcountGraph.Plots[2].PlotYAppend((theCorrelator.totalCountsCh1 - theCorrelator.totalCountsCh2) / theCorrelator.IntTime * 1000);
            CameraForm.PMTcountGraph.Plots[3].PlotYAppend(ctot / theCorrelator.IntTime * 1000);
            
            //Display total counts in correlator tab
            correlatorTotalCounts.Text = ctot.ToString();
            //Display counts/s above Graph
            double ctotn = ctot / theCorrelator.IntTime * 1000;
            CameraForm.PMTcountBox.Text = ctotn.ToString();
            //Display compensation merit value
            correlatorDecompMerit.Text = theCorrelator.decompMerit.ToString() + "+/-" + theCorrelator.decompMeritErr.ToString() + " %";

            // plot the averaged correlator data
            // if this is the first trace, just plot it, otherwise add it to the averaged data from before:
            if (prevCorrDataCh1.Length == 0)
            {
                avgCount = 1;
                CameraForm.scatterGraph3.Plots[0].PlotXY(corrbins, newCorrDataCh1);
                CameraForm.scatterGraph3.Plots[1].PlotXY(corrbins, newCorrDataCh2);
                CameraForm.scatterGraph3.Plots[2].PlotXY(corrbins, newCorrDataDiff);
                CameraForm.scatterGraph3.Plots[3].PlotXY(corrbins, newCorrDataSum);
                scatterGraphNormCorrSig.PlotXY(corrbins, newnormSig);
            }
            else
            {
                avgCount = avgCount + 1;

                for (int j = 0; j < avgCorrDataCh1.Length; j++)
                {
                    avgCorrDataCh1[j] = prevCorrDataCh1[j] + newCorrDataCh1[j];
                    avgCorrDataCh2[j] = prevCorrDataCh2[j] + newCorrDataCh2[j];
                    avgCorrDataDiff[j] = prevCorrDataDiff[j] + newCorrDataDiff[j];
                    avgCorrDataSum[j] = prevCorrDataSum[j] + newCorrDataSum[j];
                    avgnormSig[j] = avgCorrDataDiff[j] / (avgCorrDataSum[j] + 2 * avgCount * (int.Parse(textBoxBackEst.Text)));
                }

                CameraForm.scatterGraph3.Plots[0].PlotXY(corrbins, avgCorrDataCh1);
                CameraForm.scatterGraph3.Plots[1].PlotXY(corrbins, avgCorrDataCh2);
                CameraForm.scatterGraph3.Plots[2].PlotXY(corrbins, avgCorrDataDiff);
                CameraForm.scatterGraph3.Plots[3].PlotXY(corrbins, avgCorrDataSum);
                scatterGraphNormCorrSig.PlotXY(corrbins, avgnormSig);
            }

            if (SaveCorrelatorToggle.Value)
            {
                //get filename from control parameters tab
                string[] filename = GetDataFilename(2);

                System.IO.StreamWriter tw = new System.IO.StreamWriter(filename[0] + "Correlator Data " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                //double[] corrdata = CorrelatorGraph.Plots[0].GetYData();

                for (int j = 0; j < newCorrDataCh1.Length; j++)
                {
                    tw.WriteLine(newCorrDataCh1[j] + "\t" + newCorrDataCh2[j]);
                }
                tw.Write("\n");

                tw.Close();
            }

            ////////////////////////////////////////////////////
            // "Figure of merit" monitoring:
            // Depending on whether we are monitoring ion amplitude or correlator signal, plot "figure of merit" in the appropriate graph
            if (LockinFrequencySwitch.Value == true)
            {
                if (CameraForm.corrAmpLog.Plots[0].HistoryCount == 0)
                {
                    CameraForm.corrAmpLog.PlotXYAppend(0, 0);
                    CameraForm.corrAmpLog.PlotXYAppend(0, 0);
                    CameraForm.corrAmpLog.PlotXYAppend(0, 0);
                    CameraForm.corrAmpLog.PlotXYAppend(0, 0);
                }
            }
            else
            {
                if (CameraForm.corrMuLog.Plots[0].HistoryCount == 0)
                {
                    CameraForm.corrMuLog.PlotXYAppend(0, 0);
                    CameraForm.corrMuLog.PlotXYAppend(0, 0);
                    CameraForm.corrMuLog.PlotXYAppend(0, 0);
                    CameraForm.corrMuLog.PlotXYAppend(0, 0);
                }
            }

            double[] prevMicromotionDataY = CameraForm.corrMuLog.Plots[0].GetYData();
            double[] prevMicromotionDataX = CameraForm.corrMuLog.Plots[0].GetXData();
            double[] prevAmpDataY = CameraForm.corrAmpLog.Plots[0].GetYData();
            double[] prevAmpDataX = CameraForm.corrAmpLog.Plots[0].GetXData();

            int lastPtIndexMu = prevMicromotionDataX.Length - 1;
            int lastPtIndexAmp = prevAmpDataX.Length - 1;


            // Display current "amplitude" of micromotion or motion suppression on the appropriate graph:

            if (corrRecToggle.Value == false)
            {

                if (LockinFrequencySwitch.Value == true)
                {
                    double dq = Math.Abs(prevAmpDataY[lastPtIndexAmp] - prevAmpDataY[lastPtIndexAmp - 2]);
                    counterAmp++;
                    prevAmpDataY[lastPtIndexAmp] = (prevAmpDataY[lastPtIndexAmp] * (counterAmp - 1) + theCorrelator.decompMerit) / counterAmp;
                    prevAmpDataY[lastPtIndexAmp - 3] = prevAmpDataY[lastPtIndexAmp];
                    prevAmpDataY[lastPtIndexAmp - 2] = prevAmpDataY[lastPtIndexAmp] + Math.Sqrt(dq * dq * (counterAmp - 1) * (counterAmp - 1) + Math.Pow(theCorrelator.decompMeritErr, 2)) / counterAmp;
                    prevAmpDataY[lastPtIndexAmp - 1] = prevAmpDataY[lastPtIndexAmp] - Math.Sqrt(dq * dq * (counterAmp - 1) * (counterAmp - 1) + Math.Pow(theCorrelator.decompMeritErr, 2)) / counterAmp;
                    CameraForm.corrAmpLog.PlotXY(prevAmpDataX, prevAmpDataY);
                }
                else
                {
                    double dq = Math.Abs(prevMicromotionDataY[lastPtIndexMu] - prevMicromotionDataY[lastPtIndexMu - 2]);
                    counterMu++;
                    prevMicromotionDataY[lastPtIndexMu] = (prevMicromotionDataY[lastPtIndexMu] * (counterMu - 1) + theCorrelator.decompMerit) / counterMu;
                    prevMicromotionDataY[lastPtIndexMu - 3] = prevMicromotionDataY[lastPtIndexMu];
                    prevMicromotionDataY[lastPtIndexMu - 2] = prevMicromotionDataY[lastPtIndexMu] + Math.Sqrt(dq * dq * (counterMu - 1) * (counterMu - 1) + Math.Pow(theCorrelator.decompMeritErr, 2)) / counterMu;
                    prevMicromotionDataY[lastPtIndexMu - 1] = prevMicromotionDataY[lastPtIndexMu] - Math.Sqrt(dq * dq * (counterMu - 1) * (counterMu - 1) + Math.Pow(theCorrelator.decompMeritErr, 2)) / counterMu;
                    CameraForm.corrMuLog.PlotXY(prevMicromotionDataX, prevMicromotionDataY);
                }
            }
        }



////////////////////////////////////////////////////////////////////////////////



        private void CorrelatorExecuteFrmCallback2()
        {
            CorrelatorButton.BackColor = System.Drawing.Color.Gray;
        }
        private void CorrelatorExecuteFrmCallback3()
        {
            //post ID, errors
            correlatorID.Text = theCorrelator.ok.currentDeviceInformation;
            correlatorErrorMessages.Text = theCorrelator.ok.errorMsg;
            //post frequencies
            correlatorOutFreqtext.Text = theCorrelator.ok.OutFreq.ToString();
            correlatorRefFreqtext.Text = theCorrelator.ok.RefFreq.ToString();
            correlatorVCOFreqtext.Text = theCorrelator.ok.VCOFreq.ToString();
            correlatorCLKDIVISORtext.Text = Convert.ToString(Math.Round( theCorrelator.ok.P * 1000 / ncorrbins / double.Parse(LockInFreqtext1.Text) - 1, 0));
            //textBox7.Text = Convert.ToString((uint)Math.Round(theCorrelator.ok.P * 1000 / ncorrbins / double.Parse(LockInFreqtext1.Text) - 1, 0), 2);
            //textBox9.Text = Convert.ToString((UInt16)Math.Round(theCorrelator.ok.P * 1000 / ncorrbins / double.Parse(LockInFreqtext1.Text) - 1, 0), 2);
            //textBox10.Text = Convert.ToString((UInt16)(((uint)Math.Round(theCorrelator.ok.P * 1000 / ncorrbins / double.Parse(LockInFreqtext1.Text) - 1, 0))>>16), 2);
        }


        private void CorrelatorButton_Click(object sender, EventArgs e)
        {
            if (!CorrelatorThreadHelper.ShouldBeRunningFlag)
            {
                CorrelatorThreadHelper.ShouldBeRunningFlag = true;
                CorrelatorThreadHelper.theThread = new Thread(new ThreadStart(CorrelatorExecute));
                CorrelatorThreadHelper.theThread.Name = "Correlator thread";
                CorrelatorThreadHelper.theThread.Priority = ThreadPriority.BelowNormal;
                CorrelatorButton.BackColor = System.Drawing.Color.Green;
                //start scan thread
                try
                {
                    CorrelatorThreadHelper.theThread.Start();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                CorrelatorThreadHelper.ShouldBeRunningFlag = false;
                CorrelatorButton.BackColor = System.Drawing.Color.Gray;
                try
                {
                    CorrelatorThreadHelper.theThread.Abort();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }                
            }
        }

        private void SaveCorrTraceButton_Click(object sender, EventArgs e)
        {
            //get filename from control parameters tab
            string[] filename = GetDataFilename(2);

            // Uncomment the following if you want to save Correlator Settings files
            /*
             * 
            System.IO.StreamWriter tw = new System.IO.StreamWriter(filename[0] + "Correlator Settings " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");
            
            tw.WriteLine("Electrodes Settings");

            for (int i = 0; i < DCrows; i++)
                tw.WriteLine("DC" + i + "\t" + DCsliders[i].Value);
            tw.WriteLine("DX tot" + "\t" + DXSlider.Value);
            tw.WriteLine("Array tot" + "\t" + ArrayTotalSlider.Value);
            tw.WriteLine("DC vert dipole" + "\t" + DCVertDipoleSlider.Value);
            tw.WriteLine("DC quad" + "\t" + DCVertQuadSlider.Value);
            tw.WriteLine("Bias tot" + "\t" + TotalBiasSlider.Value);
            tw.WriteLine("Trap Height" + "\t" + TrapHeightSlider.Value);
            tw.WriteLine("Quadrupole Tilt" + "\t" + QuadrupoleTilt.Value);
            tw.WriteLine("Quad Tilt Ratio" + "\t" + QuadTiltRatioSlider.Value);
            tw.WriteLine("Transfer Cavity" + "\t" + TransferCavity.Value);
            tw.WriteLine("Snake Inner Ratio" + "\t" + RatioSlider.Value);
            for (int i = 0; i < DCrows; i++)
                tw.WriteLine("DC dx" + i + "\t" + DCslidersDx[i].Value);
            for (int i = 0; i < DCrows; i++)
                tw.WriteLine("DC left" + i + "\t" + DCslidersLeft[i].Value);
            for (int i = 0; i < DCrows; i++)
                tw.WriteLine("DC right" + i + "\t" + DCslidersRight[i].Value);

            tw.WriteLine("Electrode Scan Parameters");
            tw.WriteLine("DC1 Start Value" + "\t" + ElectrodeScanStartValue1Textbox.Text);
            tw.WriteLine("DC1 End Value" + "\t" + ElectrodeScanEndValue1Textbox.Text);
            tw.WriteLine("DC2 Start Value" + "\t" + ElectrodeScanStartValue2Textbox.Text);
            tw.WriteLine("DC2 End Value" + "\t" + ElectrodeScanEndValue2Textbox.Text);
            tw.WriteLine("Number of Points" + "\t" + ElectrodeScanNumPointsTextbox.Text);
            tw.WriteLine("PMT Averaging" + "\t" + ElectrodeScanPMTAveragingTextbox.Text);
            tw.WriteLine("PMT Average Background" + "\t" + PMTBackgroundAvgTextBox.Text);

            tw.Close();
             */


            System.IO.StreamWriter tw = new System.IO.StreamWriter(filename[0] + "Correlator Data " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

            double[] corrdata = CorrelatorGraph.Plots[0].GetYData();
            for (int j = 0; j < corrdata.Length; j++)
            {
                tw.WriteLine(corrdata[j]);
            }
            tw.Write("\n");

            tw.Close();
        }
        




        //
        //
        // ELECTRODE SCAN
        // 
        //

        private void ElectrodeScanStart_Click(object sender, EventArgs e)
        {
            if (!ElectrodeScanThreadHelper.ShouldBeRunningFlag)
            {
                ElectrodeScanThreadHelper.ShouldBeRunningFlag = true;
                ElectrodeScanThreadHelper.theThread = new Thread(new ThreadStart(ElectrodeScanExecute));
                ElectrodeScanThreadHelper.theThread.Name = "Electrode Scan thread";
                ElectrodeScanThreadHelper.theThread.Priority = ThreadPriority.Normal;
                ElectrodeScanThreadHelper.index = 0;
                //get scan parameters and declare data arrays
                ElectrodeScanThreadHelper.min = new double[2];
                ElectrodeScanThreadHelper.max = new double[2];
                ElectrodeScanThreadHelper.min[0] = double.Parse(ElectrodeScanStartValue1Textbox.Text);
                ElectrodeScanThreadHelper.max[0] = double.Parse(ElectrodeScanEndValue1Textbox.Text);
                ElectrodeScanThreadHelper.min[1] = double.Parse(ElectrodeScanStartValue2Textbox.Text);
                ElectrodeScanThreadHelper.max[1] = double.Parse(ElectrodeScanEndValue2Textbox.Text);
                ElectrodeScanThreadHelper.numAverage = int.Parse(ElectrodeScanPMTAveragingTextbox.Text);
                ElectrodeScanThreadHelper.numPoints = int.Parse(ElectrodeScanNumPointsTextbox.Text);
                if (ElectrodeScanThreadHelper.numPoints < 2)
                {
                    ElectrodeScanThreadHelper.numPoints = 2;
                    ElectrodeScanNumPointsTextbox.Text = "2";
                }
                //get Stream type from combo box
                ElectrodeScanThreadHelper.message = ElectrodeScanComboBox.Text;

                //define dim 2 array for PMT average and PMT sigma, and for Camera Fluorescence Data
                //if camera is running stop it
                if (ElectrodeScanThreadHelper.message == "PMT & Camera")
                {
                    ElectrodeScanThreadHelper.initDoubleData(ElectrodeScanThreadHelper.numPoints, 3, 2);
                    // if camera is running stop it
                    if (CameraThreadHelper.ShouldBeRunningFlag)
                    {
                        StopCameraThread();
                    }
                }
                else if (ElectrodeScanThreadHelper.message == "PMT")
                {
                    ElectrodeScanThreadHelper.initDoubleData(ElectrodeScanThreadHelper.numPoints, 2, 2);
                }
                else
                {
                    ElectrodeScanThreadHelper.initDoubleData(ElectrodeScanThreadHelper.numPoints, 1, 2);
                    // if camera is running stop it
                    if (CameraThreadHelper.ShouldBeRunningFlag)
                    {
                        StopCameraThread();
                    }
                }
                
                //start scan thread
                try
                {
                    ElectrodeScanThreadHelper.theThread.Start();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                ElectrodeScanThreadHelper.ShouldBeRunningFlag = false;
            }
        }
        private void ElectrodeScanExecute()
        {
            //update button
            ElectrodeScanStart.BackColor = System.Drawing.Color.White;
            //clear graph
            CameraForm.scatterGraph3.ClearData();
            //if running camera, initialize, and clear fluor and position graphs
            if (ElectrodeScanThreadHelper.message == "Camera" || ElectrodeScanThreadHelper.message == "PMT & Camera")
            {
                // clear graphs
                CameraForm.FluorescenceGraph.ClearData();
                CameraForm.PositionGraph.ClearData();
                if (Camera.AppInitialize())
                {
                    CameraInitializeHelper();
                }
            }

            if (ElectrodeScanThreadHelper.message == "Correlator:Sum")
            {
                //Initialize parameters to values entered under "Correlator" Tab  
                //if correlator returns false for init, abort scan
                if (!CorrelatorParameterInit())
                {
                    //end scan
                    ElectrodeScanThreadHelper.ShouldBeRunningFlag = false;
                    //show message
                    MessageBox.Show("Correlator Init returned false");
                }
            }
            //run scans
            while (ElectrodeScanThreadHelper.index < (ElectrodeScanThreadHelper.numPoints) && ElectrodeScanThreadHelper.ShouldBeRunningFlag)
            {
                //Compute new field values
                ElectrodeScanThreadHelper.DoubleScanVariable[0,ElectrodeScanThreadHelper.index] = (double)(ElectrodeScanThreadHelper.min[0] + (ElectrodeScanThreadHelper.max[0] - ElectrodeScanThreadHelper.min[0]) * ElectrodeScanThreadHelper.index / (ElectrodeScanThreadHelper.numPoints - 1));
                ElectrodeScanThreadHelper.DoubleScanVariable[1, ElectrodeScanThreadHelper.index] = (double)(ElectrodeScanThreadHelper.min[1] + (ElectrodeScanThreadHelper.max[1] - ElectrodeScanThreadHelper.min[1]) * ElectrodeScanThreadHelper.index / (ElectrodeScanThreadHelper.numPoints - 1));
                //call to change electrode values
                try
                {
                    this.Invoke(new MyDelegate(ElectrodeScanFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                if (ElectrodeScanThreadHelper.message == "PMT" || ElectrodeScanThreadHelper.message == "PMT & Camera")
                {
                    for (int i = 0; i < ElectrodeScanThreadHelper.numAverage; i++)
                    {
                        //GPIB PMT Counts
                        //get reading from GPIB counter
                        gpibdevice.simpleRead(21);
                        //get decimal number
                        ElectrodeScanThreadHelper.SingleDouble = gpibDoubleResult();
                        //load result in array
                        ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index] += ElectrodeScanThreadHelper.SingleDouble;
                        try
                        {
                            this.Invoke(new MyDelegate(ElectrodeScanFrmCallback));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //update Sigma array
                        ElectrodeScanThreadHelper.DoubleData[1, ElectrodeScanThreadHelper.index] += Math.Pow(ElectrodeScanThreadHelper.SingleDouble, 2);
                    }
                    //finalize single point average and standard deviation
                    ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index] = ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index] / ElectrodeScanThreadHelper.numAverage;
                    ElectrodeScanThreadHelper.DoubleData[1, ElectrodeScanThreadHelper.index] = Math.Sqrt(ElectrodeScanThreadHelper.DoubleData[1, ElectrodeScanThreadHelper.index] / ElectrodeScanThreadHelper.numAverage - Math.Pow(ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index], 2));

                    lock (ElectrodeScanThreadHelper)
                    {
                        //display count, plot
                        try
                        {
                            this.BeginInvoke(new MyDelegate(ElectrodeScanFrmCallback4));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        Monitor.Wait(ElectrodeScanThreadHelper);
                    }
                    //increase index
                    ElectrodeScanThreadHelper.index++;
                }
                // if Camera selected run Camera acquisition
                if (ElectrodeScanThreadHelper.message == "Camera" || ElectrodeScanThreadHelper.message == "PMT & Camera")
                {
                    CameraAcquisitionHelper();
                    ElectrodeScanThreadHelper.index++;
                }
                // if AI selected, get reading from NI card
                if (ElectrodeScanThreadHelper.message == "Dev3AI2")
                {
                    ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index] = Dev3AI2.ReadAnalogValue();
                    ElectrodeScanThreadHelper.index++;
                }

                // if Correlator:Sum selected, get reading from correlator, and sum bins
                if (ElectrodeScanThreadHelper.message == "Correlator:Sum")
                {
                    //Get results into correlator object
                    CorrelatorGetResultsHelper();

                    //Put sum of two channels data in Thread array
                    ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index] = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;

                    //plot
                    lock (ElectrodeScanThreadHelper)
                    {
                        //display count, plot
                        try
                        {
                            this.BeginInvoke(new MyDelegate(ElectrodeScanFrmCallback5));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        Monitor.Wait(ElectrodeScanThreadHelper);
                    }
                    //increase index
                    ElectrodeScanThreadHelper.index++;
                }
            }
            if (ElectrodeScanThreadHelper.ShouldBeRunningFlag)
            {
                //save Scan Data
                SaveScanData(ElectrodeScanThreadHelper);
                //go back to initial value
                try
                {
                    this.BeginInvoke(new MyDelegate(ElectrodeScanFrmCallback6));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

            }
            //reset button
            try
            {
                this.BeginInvoke(new MyDelegate(ElectrodeScanFrmCallback2));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            //reset scan boolean
            ElectrodeScanThreadHelper.ShouldBeRunningFlag = false;
        }
        private void ElectrodeScanFrmCallback()
        {
            //display count
            CameraForm.PMTcountBox.Text = ElectrodeScanThreadHelper.SingleDouble.ToString();
            //update PMT plot
            CameraForm.PMTcountGraph.PlotYAppend(ElectrodeScanThreadHelper.SingleDouble);
        }
        private void ElectrodeScanFrmCallback2()
        {
            ElectrodeScanStart.BackColor = System.Drawing.Color.Gainsboro;
            ElectrodeScanStart.Text = "Start *Electrode* Scan";
        }
        private void ElectrodeScanFrmCallback3()
        {
            //Compute new electrode values
            this.DCsliders[int.Parse(ElectrodeScanDC1TextBox.Text)].Value = ElectrodeScanThreadHelper.DoubleScanVariable[0, ElectrodeScanThreadHelper.index];
            this.DCsliders[int.Parse(ElectrodeScanDC2TextBox.Text)].Value = ElectrodeScanThreadHelper.DoubleScanVariable[1, ElectrodeScanThreadHelper.index];
            //update DAC
            compensationAdjustedHelper();
            //Button Indicator
            ElectrodeScanStart.Text = "Scanning..." + ElectrodeScanThreadHelper.index.ToString();
        }
        private void ElectrodeScanFrmCallback4()
        {
            lock (ElectrodeScanThreadHelper)
            {
                try
                {
                    ElectrodeScanLiveAverageTextbox.Text = ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index].ToString();
                    ElectrodeScanLiveStdTextbox.Text = ElectrodeScanThreadHelper.DoubleData[1, ElectrodeScanThreadHelper.index].ToString();
                    //plot
                    CameraForm.scatterGraph3.PlotXYAppend(ElectrodeScanThreadHelper.DoubleScanVariable[0, ElectrodeScanThreadHelper.index], ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index]);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                Monitor.PulseAll(ElectrodeScanThreadHelper);
            }
        }

        private void ElectrodeScanFrmCallback5()
        {
            lock (ElectrodeScanThreadHelper)
            {
                try
                {
                    //plot
                    CameraForm.scatterGraph3.PlotXYAppend(ElectrodeScanThreadHelper.DoubleScanVariable[0, ElectrodeScanThreadHelper.index], ElectrodeScanThreadHelper.DoubleData[0, ElectrodeScanThreadHelper.index]);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                Monitor.PulseAll(ElectrodeScanThreadHelper);
            }
        }

        private void ElectrodeScanFrmCallback6()
        {
            //reset to original values
            this.DCsliders[int.Parse(ElectrodeScanDC1TextBox.Text)].Value = ElectrodeScanThreadHelper.min[0];
            this.DCsliders[int.Parse(ElectrodeScanDC2TextBox.Text)].Value = ElectrodeScanThreadHelper.min[1];
            //update DAC
            compensationAdjustedHelper();
        }

        //
        //
        // ARBITRARY SLIDER SCAN
        // 
        //
        private void SliderScanStart_Click(object sender, EventArgs e)
        {
            if (!SliderScanThreadHelper.ShouldBeRunningFlag)
            {
                SliderScanThreadHelper.ShouldBeRunningFlag = true;
                SliderScanThreadHelper.theThread = new Thread(new ThreadStart(SliderScanExecute));
                SliderScanThreadHelper.theThread.Name = "Slider Scan thread";
                SliderScanThreadHelper.theThread.Priority = ThreadPriority.Normal;
                SliderScanThreadHelper.index = 0;
                //get scan parameters and declare data arrays
                SliderScanThreadHelper.min = new double[1];
                SliderScanThreadHelper.max = new double[1];
                SliderScanThreadHelper.min[0] = double.Parse(SliderScanStartValueTextbox.Text);
                SliderScanThreadHelper.max[0] = double.Parse(SliderScanEndValueTextbox.Text);
                SliderScanThreadHelper.numAverage = int.Parse(SliderScanPMTAveragingTextbox.Text);
                SliderScanThreadHelper.numPoints = int.Parse(SliderScanNumPointsTextbox.Text);
                //get Slider to scan
                switch (SliderScanSelection.Text)
                {
                    case "DXSlider":
                        SliderScanThreadHelper.theSlider = this.DXSlider;
                        break;
                    case "ArrayTotalSlider":
                        SliderScanThreadHelper.theSlider = this.ArrayTotalSlider;
                        break;
                    case "DCVertDipoleSlider":
                        SliderScanThreadHelper.theSlider = this.DCVertDipoleSlider;
                        break;
                    case "DCVertQuadSlider":
                        SliderScanThreadHelper.theSlider = this.DCVertQuadSlider;
                        break;
                    case "TotalBiasSlider":
                        SliderScanThreadHelper.theSlider = this.TotalBiasSlider;
                        break;
                    case "TrapHeightSlider":
                        SliderScanThreadHelper.theSlider = this.TrapHeightSlider;
                        break;
                    case "QuadrupoleTilt":
                        SliderScanThreadHelper.theSlider = this.QuadrupoleTilt;
                        break;
                    case "QuadTiltRatioSlider":
                        SliderScanThreadHelper.theSlider = this.QuadTiltRatioSlider;
                        break;
                    case "SnakeRatioSlider":
                        SliderScanThreadHelper.theSlider = this.SnakeRatioSlider;
                        break;
                    case "RightFingersSlider":
                        SliderScanThreadHelper.theSlider = this.RightFingersSlider;
                        break;
                    case "LeftFingersSlider":
                        SliderScanThreadHelper.theSlider = this.LeftFingersSlider;
                        break;
                    case "SnakeOnlySlider":
                        SliderScanThreadHelper.theSlider = this.SnakeOnlySlider;
                        break;
                    case "TransferCavity":
                        SliderScanThreadHelper.theSlider = this.TransferCavity;
                        break;
                    case "RepumperSlider":
                        SliderScanThreadHelper.theSlider = this.RepumperSlider;
                        break;
                    case "RepumperPowerSlider":
                        SliderScanThreadHelper.theSlider = this.RepumperPowerSlider;
                        break;
                    case "SideBeam370Power":
                        SliderScanThreadHelper.theSlider = this.SideBeam370Power;
                        break;
                    case "LatticePowerControl":
                        SliderScanThreadHelper.theSlider = this.LatticePowerControl;
                        break;
                    case "CavityCoolingPowerControl":
                        SliderScanThreadHelper.theSlider = this.CavityCoolingPowerControl;
                        break;
                    case "Sideband402Control":
                        SliderScanThreadHelper.theSlider = this.Sideband402Control;
                        break;
                    case "RamanSlider":
                        SliderScanThreadHelper.theSlider = this.RamanSlider;
                        break;
                    case "BxSlider":
                        SliderScanThreadHelper.theSlider = this.BxSlider;
                        break;
                    case "TickleSlider":
                        SliderScanThreadHelper.theSlider = this.TickleSlider;
                        break;
                    case "CorrelatorBinningPhaseSlider":
                        SliderScanThreadHelper.theSlider = this.CorrelatorBinningPhaseSlider;
                        break;
                }
                //modify thread name for file saving
                SliderScanThreadHelper.threadName = SliderScanThreadHelper.theSlider.Name;

                if (SliderScanThreadHelper.numPoints < 2)
                {
                    SliderScanThreadHelper.numPoints = 2;
                    SliderScanNumPointsTextbox.Text = "2";
                }
                //get Stream type from combo box
                SliderScanThreadHelper.message = SliderScanComboBox.Text;

                //define dim 2 array for PMT average and PMT sigma, and for Camera Fluorescence Data
                //if camera is running stop it
                if (SliderScanThreadHelper.message == "PMT & Camera")
                {
                    SliderScanThreadHelper.initDoubleData(SliderScanThreadHelper.numPoints, 3, 1);
                    // if camera is running stop it
                    if (CameraThreadHelper.ShouldBeRunningFlag)
                    {
                        CameraThreadHelper.ShouldBeRunningFlag = false;
                    }
                }
                else if (SliderScanThreadHelper.message == "PMT")
                {
                    SliderScanThreadHelper.initDoubleData(SliderScanThreadHelper.numPoints, 2, 1);
                }
                else
                {
                    SliderScanThreadHelper.initDoubleData(SliderScanThreadHelper.numPoints, 1, 1);
                    // if camera is running stop it
                    if (CameraThreadHelper.ShouldBeRunningFlag)
                    {
                        CameraThreadHelper.ShouldBeRunningFlag = false;
                    }
                }

                //start scan thread
                try
                {
                    SliderScanThreadHelper.theThread.Start();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                SliderScanThreadHelper.ShouldBeRunningFlag = false;
            }
        }
        private void SliderScanExecute()
        {
            //update button
            SliderScanStart.BackColor = System.Drawing.Color.White;
            //clear graph
            CameraForm.scatterGraph3.ClearData();
            //if running camera, initialize, and clear fluor and position graphs
            if (SliderScanThreadHelper.message == "Camera" || SliderScanThreadHelper.message == "PMT & Camera")
            {
                // clear graphs
                CameraForm.FluorescenceGraph.ClearData();
                CameraForm.PositionGraph.ClearData();
                if (Camera.AppInitialize())
                {
                    CameraInitializeHelper();
                }
            }

            if (SliderScanThreadHelper.message == "Correlator:Sum")
            {
                //Initialize parameters to values entered under "Correlator" Tab  
                //if correlator returns false for init, abort scan
                if (!CorrelatorParameterInit())
                {
                    //end scan
                    SliderScanThreadHelper.ShouldBeRunningFlag = false;
                    //show message
                    MessageBox.Show("Correlator Init returned false");
                }
            }

            //run scans
            while (SliderScanThreadHelper.index < (SliderScanThreadHelper.numPoints) && SliderScanThreadHelper.ShouldBeRunningFlag)
            {
                //Compute new field values
                SliderScanThreadHelper.DoubleScanVariable[0, SliderScanThreadHelper.index] = (double)(SliderScanThreadHelper.min[0] + (SliderScanThreadHelper.max[0] - SliderScanThreadHelper.min[0]) * SliderScanThreadHelper.index / (SliderScanThreadHelper.numPoints - 1));
                //call to change field value
                try
                {
                    this.Invoke(new MyDelegate(SliderScanFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                if (SliderScanThreadHelper.message == "PMT" || SliderScanThreadHelper.message == "PMT & Camera")
                {
                    for (int i = 0; i < SliderScanThreadHelper.numAverage; i++)
                    {
                        //get reading from GPIB counter
                        gpibdevice.simpleRead(21);
                        try
                        {
                            this.Invoke(new MyDelegate(SliderScanFrmCallback));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //update Sigma array
                        SliderScanThreadHelper.DoubleData[1, SliderScanThreadHelper.index] += Math.Pow(SliderScanThreadHelper.SingleDouble, 2);
                    }
                    //finalize single point average and standard deviation
                    SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index] = SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index] / SliderScanThreadHelper.numAverage;
                    SliderScanThreadHelper.DoubleData[1, SliderScanThreadHelper.index] = Math.Sqrt(SliderScanThreadHelper.DoubleData[1, SliderScanThreadHelper.index] / SliderScanThreadHelper.numAverage - Math.Pow(SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index], 2));

                    lock (SliderScanThreadHelper)
                    {
                        //display count, plot
                        try
                        {
                            this.BeginInvoke(new MyDelegate(SliderScanFrmCallback4));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        Monitor.Wait(SliderScanThreadHelper);
                    }
                    //increase index
                    SliderScanThreadHelper.index++;
                }
                // if Camera selected run Camera acquisition
                if (SliderScanThreadHelper.message == "Camera" || SliderScanThreadHelper.message == "PMT & Camera")
                {
                    CameraAcquisitionHelper();
                    SliderScanThreadHelper.index++;
                }

                // if AI selected, get reading from NI card
                if (SliderScanThreadHelper.message == "Dev3AI2")
                {
                    SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index] = Dev3AI2.ReadAnalogValue();
                    SliderScanThreadHelper.index++;
                }

                // if Correlator:Sum selected, get reading from correlator, and sum bins
                if (SliderScanThreadHelper.message == "Correlator:Sum")
                {
                    //Get results into correlator object
                    CorrelatorGetResultsHelper();

                    //Put sum of two channels data in Thread array
                    SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index] = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;

                    //plot
                    lock (SliderScanThreadHelper)
                    {
                        //display count, plot
                        try
                        {
                            this.BeginInvoke(new MyDelegate(SliderScanFrmCallback5));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        Monitor.Wait(SliderScanThreadHelper);
                    }
                    //increase index
                    SliderScanThreadHelper.index++;
                }
            }
            if (SliderScanThreadHelper.ShouldBeRunningFlag)
            {
                //save Scan Data
                SaveScanData(SliderScanThreadHelper);
            }
            //go back to initial value
            //reset button
            try
            {
                this.Invoke(new MyDelegate(SliderScanFrmCallback2));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            //reset scan boolean
            SliderScanThreadHelper.ShouldBeRunningFlag = false;
        }
        private void SliderScanFrmCallback()
        {
            //get decimal number
            SliderScanThreadHelper.SingleDouble = gpibDoubleResult();
            //load result in array
            SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index] += SliderScanThreadHelper.SingleDouble;
            //display count
            CameraForm.PMTcountBox.Text = SliderScanThreadHelper.SingleDouble.ToString();
            //update PMT plot
            CameraForm.PMTcountGraph.PlotYAppend(SliderScanThreadHelper.SingleDouble);
        }
        private void SliderScanFrmCallback2()
        {
            SliderScanStart.BackColor = System.Drawing.Color.WhiteSmoke;
            SliderScanStart.Text = "Start *Slider* Scan";
            //reset to original values
            SliderScanThreadHelper.theSlider.Value = SliderScanThreadHelper.min[0];
        }
        private void SliderScanFrmCallback3()
        {
            //update slider
            SliderScanThreadHelper.theSlider.Value = SliderScanThreadHelper.DoubleScanVariable[0, SliderScanThreadHelper.index];
            //Button Indicator
            SliderScanStart.Text = "Scanning..." + SliderScanThreadHelper.index.ToString();
            //update DAC
            UpdateAll();
        }
        private void SliderScanFrmCallback4()
        {
            lock (SliderScanThreadHelper)
            {
                try
                {
                    SliderScanLiveAverageTextbox.Text = SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index].ToString();
                    SliderScanLiveStdTextbox.Text = SliderScanThreadHelper.DoubleData[1, SliderScanThreadHelper.index].ToString();
                    //plot
                    CameraForm.scatterGraph3.PlotXYAppend(SliderScanThreadHelper.DoubleScanVariable[0, SliderScanThreadHelper.index], SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index]);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                Monitor.PulseAll(SliderScanThreadHelper);
            }
        }

        private void SliderScanFrmCallback5()
        {
            lock (SliderScanThreadHelper)
            {
                try
                {
                    //plot
                    CameraForm.scatterGraph3.PlotXYAppend(SliderScanThreadHelper.DoubleScanVariable[0, SliderScanThreadHelper.index], SliderScanThreadHelper.DoubleData[0, SliderScanThreadHelper.index]);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                Monitor.PulseAll(SliderScanThreadHelper);
            }
        }

        //
        //
        // FLUORESCENCE LOG THREAD
        // 
        //
        private void FluorLogStart_Click(object sender, EventArgs e)
        {
            if (!FluorLogThreadHelper.ShouldBeRunningFlag)
            {
                FluorLogThreadHelper.ShouldBeRunningFlag = true;
                FluorLogThreadHelper.theThread = new Thread(new ThreadStart(FluorLogExecute));
                FluorLogThreadHelper.theThread.Name = "Fluor Log thread";
                FluorLogThreadHelper.theThread.Priority = ThreadPriority.Normal;
                FluorLogThreadHelper.index = 0;
                //get scan parameters and declare data arrays
                FluorLogThreadHelper.numPoints = int.Parse(FluorLogNumPointsTextbox.Text);
                if (FluorLogThreadHelper.numPoints < 2)
                {
                    FluorLogThreadHelper.numPoints = 2;
                    FluorLogNumPointsTextbox.Text = "2";
                }
                //get Stream type from combo box
                FluorLogThreadHelper.message = FluorLogComboBox.Text;

                //define dim 2 array for PMT average and PMT sigma, and for Camera Fluorescence Data
                //if camera is running stop it
                if (FluorLogThreadHelper.message == "PMT")
                {
                    FluorLogThreadHelper.initDoubleData(FluorLogThreadHelper.numPoints, 2, 1);
                }
                else
                {
                    FluorLogThreadHelper.initDoubleData(FluorLogThreadHelper.numPoints, 1, 1);
                    // if camera is running stop it
                    if (CameraThreadHelper.ShouldBeRunningFlag)
                    {
                        CameraThreadHelper.ShouldBeRunningFlag = false;
                    }
                }

                //start scan thread
                try
                {
                    FluorLogThreadHelper.theThread.Start();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                FluorLogThreadHelper.ShouldBeRunningFlag = false;
            }
        }
        private void FluorLogExecute()
        {
            //update button
            FluorLogStart.BackColor = System.Drawing.Color.White;
            //clear graph
            CameraForm.scatterGraph3.ClearData();
            //if running camera, initialize, and clear fluor and position graphs
            if (FluorLogThreadHelper.message == "Camera")
            {
                // clear graphs
                CameraForm.FluorescenceGraph.ClearData();
                CameraForm.PositionGraph.ClearData();
                if (Camera.AppInitialize())
                {
                    CameraInitializeHelper();
                }
            }
            if (FluorLogThreadHelper.message == "Correlator:Sum")
            {
                //Initialize parameters to values entered under "Correlator" Tab  
                //if correlator returns false for init, abort scan
                if (!CorrelatorParameterInit())
                {
                    //end scan
                    FluorLogThreadHelper.ShouldBeRunningFlag = false;
                    //show message
                    MessageBox.Show("Correlator Init returned false");
                }
            }
            //run scans
            while (FluorLogThreadHelper.index < (FluorLogThreadHelper.numPoints) && FluorLogThreadHelper.ShouldBeRunningFlag)
            {
                //Update Scan variable
                FluorLogThreadHelper.DoubleScanVariable[0, FluorLogThreadHelper.index] = (double)FluorLogThreadHelper.index;
                //call to change button
                try
                {
                    this.Invoke(new MyDelegate(FluorLogFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                if (FluorLogThreadHelper.message == "PMT")
                {
                    for (int i = 0; i < FluorLogThreadHelper.numAverage; i++)
                    {
                        //get reading from GPIB counter
                        gpibdevice.simpleRead(21);
                        try
                        {
                            this.Invoke(new MyDelegate(FluorLogFrmCallback));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //update Sigma array
                        FluorLogThreadHelper.DoubleData[1, FluorLogThreadHelper.index] += Math.Pow(FluorLogThreadHelper.SingleDouble, 2);
                    }
                    //finalize single point average and standard deviation
                    FluorLogThreadHelper.DoubleData[0, FluorLogThreadHelper.index] = FluorLogThreadHelper.DoubleData[0, FluorLogThreadHelper.index] / FluorLogThreadHelper.numAverage;
                    FluorLogThreadHelper.DoubleData[1, FluorLogThreadHelper.index] = Math.Sqrt(FluorLogThreadHelper.DoubleData[1, FluorLogThreadHelper.index] / FluorLogThreadHelper.numAverage - Math.Pow(FluorLogThreadHelper.DoubleData[0, FluorLogThreadHelper.index], 2));
                    //plot
                    CameraForm.scatterGraph3.PlotXYAppend(FluorLogThreadHelper.DoubleScanVariable[0, FluorLogThreadHelper.index], FluorLogThreadHelper.DoubleData[0, FluorLogThreadHelper.index]);
                    //increase index
                    FluorLogThreadHelper.index++;
                }
                // if Camera selected run Camera acquisition
                if (FluorLogThreadHelper.message == "Camera")
                {
                    CameraAcquisitionHelper();
                    FluorLogThreadHelper.index++;
                }

                // if Correlator:Sum selected, get reading from correlator, and sum bins
                if (FluorLogThreadHelper.message == "Correlator:Sum")
                {
                    //Get results into correlator object
                    CorrelatorGetResultsHelper();

                    //Put sum of two channels data in Thread array
                    FluorLogThreadHelper.DoubleData[0, FluorLogThreadHelper.index] = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;

                    //plot
                    lock (FluorLogThreadHelper)
                    {
                        //display count, plot
                        try
                        {
                            this.BeginInvoke(new MyDelegate(FluorLogFrmCallback6));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        Monitor.Wait(FluorLogThreadHelper);
                    }
                    //increase index
                    FluorLogThreadHelper.index++;
                }    
            }
            if (FluorLogThreadHelper.ShouldBeRunningFlag)
            {
                //save Scan Data
                SaveScanData(FluorLogThreadHelper);
            }
            //calculate mean and standard error on the mean for the data
            try
            {
                this.Invoke(new MyDelegate(FluorLogFrmCallback5));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            //reset button
            try
            {
                this.Invoke(new MyDelegate(FluorLogFrmCallback2));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            //reset scan boolean
            FluorLogThreadHelper.ShouldBeRunningFlag = false;
        }
        private void FluorLogFrmCallback()
        {
            //get decimal number
            FluorLogThreadHelper.SingleDouble = gpibDoubleResult();
            //load result in array
            FluorLogThreadHelper.DoubleData[0, FluorLogThreadHelper.index] += FluorLogThreadHelper.SingleDouble;
            //display count
            CameraForm.PMTcountBox.Text = FluorLogThreadHelper.SingleDouble.ToString();
            //update PMT plot
            CameraForm.PMTcountGraph.PlotYAppend(FluorLogThreadHelper.SingleDouble);
        }
        private void FluorLogFrmCallback2()
        {
            FluorLogStart.BackColor = System.Drawing.Color.WhiteSmoke;
            FluorLogStart.Text = "Start LOG";
        }
        private void FluorLogFrmCallback3()
        {
            //Button Indicator
            FluorLogStart.Text = "Logging..." + FluorLogThreadHelper.index.ToString();
        }
        private void FluorLogFrmCallback5()
        {
            double mean = 0;
            for (int i = 0; i < FluorLogThreadHelper.numPoints; i++)
                mean += FluorLogThreadHelper.DoubleData[0, i];
            mean = mean / FluorLogThreadHelper.numPoints;

            double std = 0;
            for (int i = 0; i < FluorLogThreadHelper.numPoints; i++)
                std += (FluorLogThreadHelper.DoubleData[0, i] - mean) * (FluorLogThreadHelper.DoubleData[0, i] - mean);
            std = Math.Sqrt(std) / FluorLogThreadHelper.numPoints;

            FluorLogLiveAverageTextbox.Text = mean.ToString("F1");
            FluorLogLiveStdTextbox.Text = std.ToString("F1");
        }
        private void FluorLogFrmCallback6()
        {
            lock (FluorLogThreadHelper)
            {
                try
                {
                    //plot
                    CameraForm.scatterGraph3.PlotXYAppend((double) FluorLogThreadHelper.index, FluorLogThreadHelper.DoubleData[0, FluorLogThreadHelper.index]);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                Monitor.PulseAll(FluorLogThreadHelper);
            }
        }

        //
        // CAMERA THREAD
        // 

        //Updates Camera Class with parameters taken from the Form
        private void CameraParametersUpdate()
        {
            try
            {
                //set values from form and correct inconsistencies
                //exposure, no less than 0.01, maximum protected internally
                float fExposure = float.Parse(CameraExposure.Text);
                if (fExposure < 0.01f)
                {
                    fExposure = 0.01f;
                    CameraExposure.Text = fExposure.ToString();
                }
                Camera.fExposure = fExposure;
                //there is internal protection for EMGain in Camera Class
                Camera.EMGain = int.Parse(CameraEMGain.Text);
                //Here we must ensure that the image size is right given binning
                //Horizontal
                int hbin = int.Parse(CameraHbin.Text);
                int hstart = int.Parse(CameraHstart.Text);
                int hend = int.Parse(CameraHend.Text);
                while (((hend - hstart + 1) % hbin) > 0)
                {
                    if (hend < 1000) { hend++; }
                    else if (hstart > 1) { hstart--; }
                    else { hbin++; }
                }
                //update camera parameter values
                Camera.hbin = hbin;
                Camera.hstart = hstart;
                Camera.hend = hend;
                //Vertical
                int vbin = int.Parse(CameraVbin.Text);
                int vstart = int.Parse(CameraVstart.Text);
                int vend = int.Parse(CameraVend.Text);
                while (((vend - vstart + 1) % vbin) > 0)
                {
                    if (vend < 1000) { vend++; }
                    else if (vstart > 1) { vstart--; }
                    else { vbin++; }
                }
                //update camera parameter values
                Camera.vbin = vbin;
                Camera.vstart = vstart;
                Camera.vend = vend;
                //update text boxes with changed values
                try
                {
                    this.BeginInvoke(new MyDelegate(CameraThreadFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //Helper function for the fluorescence log
        private void FluorLogsHelper()
        {
            int x1 = 0, x2 = 0, y1 = 0, y2 = 0;

            //count sets of cursors
            int NumSet = (int)Math.Floor((double)CameraForm.intensityGraph1.Cursors.Count / 2);
            //count sets of fluorescence plots available
            int NumFluorPlot = CameraForm.FluorescenceGraph.Plots.Count;
            int NumPositionPlot = CameraForm.PositionGraph.Plots.Count;
            //for each set of cursor get total intensity from data
            double sum = 0;
            //define a copy of the data for Center of Mass Calculation
            //double[,] DataDoubleCopy = Camera.DataDouble;
            //multiplier applied to background to threshold fluorescence
            double multiplier = double.Parse(CameraForm.BackgroundMultiplierTextbox.Text);
            //center of mass variable
            double position = 0;
            
            //variables for ion focus optimization, indices of max fluorescence pixel
            int xmax = 0, ymax = 0;
            double fmax = 0;

            //double[,] myArray = new double[,] { { 1, 2 }, { 3, 4 }, { 5, 6 }, { 7, 8 } };
            //CameraForm.intensityGraph1.Plot(myArray);

            for (int i = 0; i < NumSet; i++)
            {
                sum = 0;

                //get cursor positions 
                x1 = (int)CameraForm.intensityGraph1.Cursors[2 * i].XPosition;
                y1 = (int)CameraForm.intensityGraph1.Cursors[2 * i].YPosition;
                x2 = (int)CameraForm.intensityGraph1.Cursors[2 * i + 1].XPosition;
                y2 = (int)CameraForm.intensityGraph1.Cursors[2 * i + 1].YPosition;

                //switch if order reversed
                if (x1 > x2) { int temp = x1; x1 = x2; x2 = temp; }
                if (y1 > y2) { int temp = y1; y1 = y2; y2 = temp; }

                if (CameraThreadHelper.DoubleData.Length >= ((y2 - y1 + 1) * (x2 - x1 + 1)))
                {
                    //Figure out background level
                    CameraThreadHelper.Background = 0;
                    //Figure out which background method is desired
                    try
                    {
                        this.Invoke(new MyDelegate(CameraThreadFrmCallback6));
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    //First choice is Rim
                    if (CameraThreadHelper.message == "Rim of intensity box")
                    {
                        for (int j = x1; j <= x2; j++)
                        {
                            CameraThreadHelper.Background += CameraThreadHelper.DoubleData[j, y1];
                            CameraThreadHelper.Background += CameraThreadHelper.DoubleData[j, y2];
                        }
                        for (int j = y1 + 1; j <= x2 - 1; j++)
                        {
                            CameraThreadHelper.Background += CameraThreadHelper.DoubleData[x1, j];
                            CameraThreadHelper.Background += CameraThreadHelper.DoubleData[x2, j];
                        }
                        CameraThreadHelper.Background = CameraThreadHelper.Background / (2 * (y2 - y1 + 1) + 2 * (x2 - x1));
                    }
                    //Second choice is duplicate box
                    else if (CameraThreadHelper.message == "Duplicate shifted box")
                    {
                        if (CameraThreadHelper.DoubleData.Length >= (2 * (y2 - y1 + 1) * (x2 - x1 + 1)))
                        {
                            for (int j = 2 * x1 - x2; j <= x1; j++)
                            {
                                for (int k = y1; k <= y2; k++)
                                {
                                    CameraThreadHelper.Background += CameraThreadHelper.DoubleData[j, k];
                                }
                            }
                            CameraThreadHelper.Background = CameraThreadHelper.Background / ((y2 - y1 + 1) * (x2 - x1 + 1));
                        }
                        else
                        {
                            MessageBox.Show("image size not sufficient for this background method");
                        }
                    }
                    //update background box
                    try
                    {
                        this.Invoke(new MyDelegate(CameraThreadFrmCallback4));
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }

                    //sum pixels from one cursor to the other
                    //the pixels under the cursor are included
                    for (int j = x1; j <= x2; j++)
                    {
                        for (int k = y1; k <= y2; k++)
                        {
                            sum += CameraThreadHelper.DoubleData[j, k];
                            //find index of max fluor pixel
                            if (fmax < CameraThreadHelper.DoubleData[j, k])
                            {
                                fmax = CameraThreadHelper.DoubleData[j, k];
                                xmax = j; ymax = k;
                            }
                            //Threshold the fluorescence
                            CameraThreadHelper.DoubleData[j, k] -= CameraThreadHelper.Background * multiplier;
                            if (CameraThreadHelper.DoubleData[j, k] < 0)
                            {
                                CameraThreadHelper.DoubleData[j, k] = 0;
                            }
                        }
                    }

                    //bin the data horizontally for vertical position
                    for (int k = y1; k <= y2; k++)
                    {
                        for (int j = x1 + 1; j <= x2; j++)
                        {
                            CameraThreadHelper.DoubleData[x1, k] += CameraThreadHelper.DoubleData[j, k];
                        }
                        //calculate center of mass before normalization
                        position += CameraThreadHelper.DoubleData[x1, k] * (k + 1 - y1);
                    }
                    //normalize position, and multiply by size of pixel
                    position = SizeOfPixel * position / sum;

                    //if enough plots have been defined, plot the sum
                    CameraThreadHelper.SingleDouble = sum;
                    CameraThreadHelper.index = i;
                    if (i < NumFluorPlot)
                    {
                        try
                        {
                            this.BeginInvoke(new MyDelegate(CameraFormThreadCallBack1));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                    //update position
                    CameraThreadHelper.SingleDouble2 = position;
                    if (i < NumPositionPlot)
                    {
                        try
                        {
                            this.BeginInvoke(new MyDelegate(CameraFormThreadCallBack2));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }

                    //Ion focus logs
                    if (CameraForm.FocusLogsONCheck.Checked)
                    {
                        //find assymetry in x
                        CameraThreadHelper.DoubleArray[0] = (CameraThreadHelper.DoubleData[xmax - 1, ymax] - CameraThreadHelper.DoubleData[xmax + 1, ymax]) / (CameraThreadHelper.DoubleData[xmax - 1, ymax] + CameraThreadHelper.DoubleData[xmax + 1, ymax]);
                        try
                        {
                            this.BeginInvoke(new MyDelegate(CameraFormThreadCallBack3));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //find spread in x
                        CameraThreadHelper.DoubleArray[1] = (CameraThreadHelper.DoubleData[xmax - 1, ymax] + CameraThreadHelper.DoubleData[xmax + 1, ymax]) / CameraThreadHelper.DoubleData[xmax, ymax];
                        try
                        {
                            this.BeginInvoke(new MyDelegate(CameraFormThreadCallBack4));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //find assymetry in y
                        CameraThreadHelper.DoubleArray[2] = (CameraThreadHelper.DoubleData[xmax, ymax - 1] - CameraThreadHelper.DoubleData[xmax, ymax + 1]) / (CameraThreadHelper.DoubleData[xmax, ymax - 1] + CameraThreadHelper.DoubleData[xmax, ymax + 1]);
                        try
                        {
                            this.BeginInvoke(new MyDelegate(CameraFormThreadCallBack5));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //find spread in x
                        CameraThreadHelper.DoubleArray[3] = (CameraThreadHelper.DoubleData[xmax, ymax - 1] + CameraThreadHelper.DoubleData[xmax, ymax + 1]) / CameraThreadHelper.DoubleData[xmax, ymax];
                        try
                        {
                            this.BeginInvoke(new MyDelegate(CameraFormThreadCallBack6));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                    }
                }
                else
                {
                    MessageBox.Show("Camera Data array too small for cursors");
                }
            }
            

            //MessageBox.Show(sum.ToString());

        }
        private void CameraFormThreadCallBack1()
        {
            CameraForm.FluorescenceGraph.Plots[CameraThreadHelper.index].PlotYAppend(CameraThreadHelper.SingleDouble);
        }
        private void CameraFormThreadCallBack2()
        {
            CameraForm.PositionGraph.Plots[CameraThreadHelper.index].PlotYAppend(CameraThreadHelper.SingleDouble2);
        }
        private void CameraFormThreadCallBack3()
        {
            CameraForm.xBalanceGraph.PlotYAppend(CameraThreadHelper.DoubleArray[0]);
        }
        private void CameraFormThreadCallBack4()
        {
            CameraForm.xSpreadGraph.PlotYAppend(CameraThreadHelper.DoubleArray[1]);
        }
        private void CameraFormThreadCallBack5()
        {
            CameraForm.yBalanceGraph.PlotYAppend(CameraThreadHelper.DoubleArray[2]);
        }
        private void CameraFormThreadCallBack6()
        {
            CameraForm.ySpreadGraph.PlotYAppend(CameraThreadHelper.DoubleArray[3]);
        }


        //For Debugging with no camera connected
        private void CameraGetAcquisitionDebug()
        {
            Camera.variableChangeFlag = true;

            for (int i = 0; i < 1000; i++)
            {
                for (int j = 0; j < 1000; j++)
                {
                    Camera.DataDouble[i, j] = i + j;
                }
            }

            Camera.variableChangeFlag = false;
        }

        public void CameraThreadExecute()
        {
            //set process priority 
            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.Normal;

            if (Camera.AppInitialize()|| DebugCheckbox.Checked)
            {
                if(!DebugCheckbox.Checked)
                {
                    CameraInitializeHelper();
                }
                else
                {
                    Camera.DataDouble = new double[1000, 1000];
                }

                while (CameraThreadHelper.ShouldBeRunningFlag)
                {
                    if (DebugCheckbox.Checked)
                    {
                        CameraGetAcquisitionDebug();
                    }
                    if(!DebugCheckbox.Checked)
                    {

                        Camera.sStatusMsg = String.Empty;

                        try
                        {
                            this.BeginInvoke(new MyDelegate(CameraThreadFrmCallback5));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        if (CameraThreadHelper.flag)
                        {
                            CameraParametersUpdate();
                            Camera.AppSetSequence();
                            lock (CameraThreadHelper.DoubleData)
                            {
                                CameraThreadHelper.DoubleData = new double[Camera.xImageSize, Camera.yImageSize];
                            }
                            CameraThreadHelper.flag = false;
                        }

                        //acquire data, display logs
                        CameraAcquisitionHelper();

                        /*
                        //stopwatch.Stop();
                        try
                        {
                            this.BeginInvoke(new MyDelegate(DebugFrmCallback));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //stopwatch.Reset();
                         */
                        
                    }
                }
            }

            //post init error message
            //update button
            try
            {
                this.Invoke(new MyDelegate(CameraThreadFrmCallback2));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        private void CameraThreadFrmCallback()
        {
            //update status box
            CameraForm.richTextBox1.Text = Camera.sStatusMsg;
        }
        private void CameraThreadFrmCallback2()
        {
            //update message status box
            CameraForm.richTextBox1.Text = Camera.sStatusMsg;
            //update button
            CameraStartButton.BackColor = System.Drawing.Color.Linen;
            CameraStartButton.Text = "Start Camera";
        }
        private void CameraThreadFrmCallback3()
        {
            //update textboxes for camera parameters
            CameraExposure.Text = Camera.fExposure.ToString();
            CameraHbin.Text = Camera.hbin.ToString();
            CameraVbin.Text = Camera.vbin.ToString();
            CameraHstart.Text = Camera.hstart.ToString();
            CameraVstart.Text = Camera.vstart.ToString();
            CameraHend.Text = Camera.hend.ToString();
            CameraVend.Text = Camera.vend.ToString();
        }
        private void CameraThreadFrmCallback4()
        {
            //update background box
            CameraForm.BackgroundTextbox.Text = CameraThreadHelper.Background.ToString("F2");
        }
        private void CameraThreadFrmCallback5()
        {
            //get temperature
            if (CameraTemperatureCheckbox.Checked)
            {
                int temp = Camera.GetTemperature();
                CameraTemperatureText.Text = temp.ToString();
            }
        }
        private void CameraThreadFrmCallback6()
        {
            CameraThreadHelper.message = CameraForm.BackgroundComboBox.Text;
        }
        private void CameraInitializeHelper()
        {
            //update camera parameters
            CameraParametersUpdate();
            //run camera set sequence for parameters
            Camera.AppSetSequence();
            //init data copy
            CameraThreadHelper.DoubleData = new double[Camera.xImageSize, Camera.yImageSize];
        }
        private void CameraAcquisitionHelper()
        {
            //get data (includes call to wait function, and timeout)
            //stopwatch.Start();
            lock (Camera)
            {
                Camera.GetSingleAcquisition();

                Array.Copy(Camera.DataDouble, CameraThreadHelper.DoubleData, Camera.DataDouble.Length);

                //Monitor.PulseAll(Camera);
            }

            //pass intensity update to main form
            try
            {
                this.Invoke(new MyDelegate(IntensityPlotUpdate));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            //update logs
            try
            {
                if (CameraForm.FluorescenceGraph.Plots.Count > 0)
                {
                    //update fluorlog
                    FluorLogsHelper();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            //update camera status box
            try
            {
                this.BeginInvoke(new MyDelegate(CameraThreadFrmCallback));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }


        private void CameraStartButton_Click(object sender, EventArgs e)
        {
            if (!CameraThreadHelper.ShouldBeRunningFlag)
            {
                StartCameraThread();
            }
            else
            {
                StopCameraThread();                
            }
        }

        private void StartCameraThread()
        {
            CameraForm.richTextBox1.Text = "Attempting Camera Initialization...";

            CameraThreadHelper.ShouldBeRunningFlag = true;
            CameraThreadHelper.theThread = new Thread(new ThreadStart(CameraThreadExecute));
            CameraThreadHelper.theThread.Name = "Camera thread";
            CameraThreadHelper.theThread.Priority = ThreadPriority.Normal;
            //update button
            CameraStartButton.BackColor = System.Drawing.Color.IndianRed;
            CameraStartButton.Text = "Stop Camera";
            //start camera thread
            CameraThreadHelper.theThread.Start();
            //Camera Time out thread
            CameraTimeOutThreadHelper.ShouldBeRunningFlag = true;
            CameraTimeOutThreadHelper.theThread = new Thread(new ThreadStart(CameraTimeOutThreadExecute));
            CameraTimeOutThreadHelper.theThread.Name = "CameraTimeOut thread";
            CameraTimeOutThreadHelper.theThread.Priority = ThreadPriority.Lowest;
            //start timeout thread
            CameraTimeOutThreadHelper.theThread.Start();
            /*
            //Intensity graph update thread
            IntensityGraphUpdateThreadHelper.ShouldBeRunningFlag = true;
            IntensityGraphUpdateThreadHelper.theThread = new Thread(new ThreadStart(IntensityGraphUpdateThreadExecute));
            IntensityGraphUpdateThreadHelper.theThread.Name = "Intensity Graph thread";
            IntensityGraphUpdateThreadHelper.theThread.Priority = ThreadPriority.BelowNormal;
            IntensityGraphUpdateThreadHelper.theThread.Start();
            */
        }

        private void StopCameraThread()
        {
            CameraStartButton.BackColor = System.Drawing.Color.Linen;
            CameraStartButton.Text = "Start Camera";
            //abort acquisition 
            Camera.Abort();
            //reset thread
            CameraThreadHelper.ShouldBeRunningFlag = false;
            IntensityGraphUpdateThreadHelper.ShouldBeRunningFlag = false;
            CameraTimeOutThreadHelper.ShouldBeRunningFlag = false;
            Camera.running = false;
        }

        private void IntensityPlotUpdate()
        {
            //set intensity update to higher process priority
            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.AboveNormal;
            //Intensity Plot Update
            CameraForm.intensityPlot1.Plot(CameraThreadHelper.DoubleData);
            //reset priority level to normal
            //set intensity update to higher process priority
            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.Normal;
        }


        private void IntensityGraphUpdateThreadExecute()
        {
            //set process priority 
            using (Process p = Process.GetCurrentProcess())
              p.PriorityClass = ProcessPriorityClass.BelowNormal;

            while (IntensityGraphUpdateThreadHelper.ShouldBeRunningFlag)
            {
                if (Camera != null)
                {
                    lock (Camera)
                    {
                        //Monitor.Wait(Camera);

                        if (Camera.running)
                        {
                            lock (CameraThreadHelper.DoubleData)
                            {
                                CameraForm.intensityPlot1.Plot(CameraThreadHelper.DoubleData);
                            }
                        }
                    }
                }
            }
        }

        public void CameraTimeOutThreadExecute()
        {
            //set process priority 
            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.BelowNormal;

            while (CameraTimeOutThreadHelper.ShouldBeRunningFlag && CameraThreadHelper.ShouldBeRunningFlag)
            {
                if (Camera != null)
                {
                    if (Camera.errorFlag)
                    {
                        StopCameraThread();
                        Camera.errorFlag = false;
                    }
                }
            }
        }

        private void DebugFrmCallback()
        {
            stopwatchTextbox.Text = Camera.stopwatch.Elapsed.ToString();
            CameraForm.PMTcountGraph.PlotYAppend((double)(Camera.stopwatch.Elapsed.Milliseconds + 1000*Camera.stopwatch.Elapsed.Seconds));
        }

        private void corrRecToggle_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (corrRecToggle.Value == false)
            {
                            
                            
                if (LockinFrequencySwitch.Value == true)
                {
                    counterAmp = 0;
                    int nextx = CameraForm.corrAmpLog.Plots[0].HistoryCount;
                    CameraForm.testlbl.Text = nextx.ToString();
                    double[] xnew = new double[4] { nextx, nextx, nextx, nextx };
                    double[] ynew = new double[4] { 0, 0, 0, 0 };
                    CameraForm.corrAmpLog.PlotXYAppend(xnew, ynew);
                }
                else
                {
                    counterMu = 0;
                    int nextx = CameraForm.corrMuLog.Plots[0].HistoryCount;
                    CameraForm.testlbl.Text = nextx.ToString();
                    double[] xnew = new double[4] { nextx, nextx, nextx, nextx };
                    double[] ynew = new double[4] { 0, 0, 0, 0 };
                    CameraForm.corrMuLog.PlotXYAppend(xnew, ynew);
                }

            }

        }

        private void clrCorrLog_Click(object sender, EventArgs e)
        {
            if (LockinFrequencySwitch.Value == true)
                CameraForm.corrAmpLog.ClearData();
            else
                CameraForm.corrMuLog.ClearData();
        }

        private void groupBox51_Enter(object sender, EventArgs e)
        {

        }

        private void switchDisplayBases_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (switchDisplayBases.Value)
            {
                CameraForm.PMTcountGraph.Plots[0].Visible = true;
                CameraForm.PMTcountGraph.Plots[1].Visible = true;
                CameraForm.PMTcountGraph.Plots[2].Visible = false;
                CameraForm.PMTcountGraph.Plots[3].Visible = false;


                CameraForm.scatterGraph3.Plots[0].Visible = true;
                CameraForm.scatterGraph3.Plots[1].Visible = true;
                CameraForm.scatterGraph3.Plots[2].Visible = false;
                CameraForm.scatterGraph3.Plots[3].Visible = false;

                CorrelatorGraph.Plots[0].Visible = true;
                CorrelatorGraph.Plots[1].Visible = true;
                CorrelatorGraph.Plots[2].Visible = false;
                CorrelatorGraph.Plots[3].Visible = false;
            }
            else
            {
                CameraForm.PMTcountGraph.Plots[0].Visible = false;
                CameraForm.PMTcountGraph.Plots[1].Visible = false;
                CameraForm.PMTcountGraph.Plots[2].Visible = true;
                CameraForm.PMTcountGraph.Plots[3].Visible = true;


                CameraForm.scatterGraph3.Plots[0].Visible = false;
                CameraForm.scatterGraph3.Plots[1].Visible = false;
                CameraForm.scatterGraph3.Plots[2].Visible = true;
                CameraForm.scatterGraph3.Plots[3].Visible = true;

                CorrelatorGraph.Plots[0].Visible = false;
                CorrelatorGraph.Plots[1].Visible = false;
                CorrelatorGraph.Plots[2].Visible = true;
                CorrelatorGraph.Plots[3].Visible = true;
            }

        }




        // Re: Correlator integration time parameter selector updated
        // Event that responds to a change of the correlator integration time selector switch to call for update of integration time via OK wires through the correlator object
        private void intTselector_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (intTselector.Value)
            {
                theCorrelator.IntTime = int.Parse(correlatorIntTimetext1.Text);
            }
            else
            {
                theCorrelator.IntTime = int.Parse(correlatorIntTimetext2.Text);
            }
            theCorrelator.updateCorrIntTimeLive();
        }

        // Re: Correlator integration time input 1 updated
        // Event that responds to a change of the correlator integration time in input box 1 to call for update via OK wires through the correlator object
        private void correlatorIntTimetext1_TextChanged(object sender, EventArgs e)
        {
            if (intTselector.Value)
            {
                theCorrelator.IntTime = int.Parse(correlatorIntTimetext1.Text);
                theCorrelator.updateCorrIntTimeLive();
            }
        }

        // Re: Correlator integration time input 2 updated
        // Event that responds to a change of the correlator integration time in input box 2 to call for update via OK wires through the correlator object
        private void correlatorIntTimetext2_TextChanged(object sender, EventArgs e)
        {
            if (!intTselector.Value)
            {
                theCorrelator.IntTime = int.Parse(correlatorIntTimetext2.Text);
                theCorrelator.updateCorrIntTimeLive();
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////// Conversion between kHz and microseconds in textboxes /////////////////////////////
        private void LockInFreqtext1_TextChanged(object sender, EventArgs e)
        {
            double lockinfreq1Val = Double.Parse(LockInFreqtext1.Text); // in kHz
            double lockinper1Val = 1/lockinfreq1Val*1000; // in microseconds
            LockInPertext1.Text = lockinper1Val.ToString();
        }
        private void LockInPertext1_TextChanged(object sender, EventArgs e)
        {
            double lockinper1Val = Double.Parse(LockInPertext1.Text); // in kHz
            double lockinfreq1Val = 1 / lockinper1Val * 1000; // in microseconds
            LockInFreqtext1.Text = lockinfreq1Val.ToString();
        }
        private void LockInFreqtext2_TextChanged(object sender, EventArgs e)
        {
            double lockinfreq2Val = Double.Parse(LockInFreqtext2.Text); // in kHz
            double lockinper2Val = 1 / lockinfreq2Val * 1000; // in microseconds
            LockInPertext2.Text = lockinper2Val.ToString();
        }
        private void LockInPerqtext2_TextChanged(object sender, EventArgs e)
        {
            double lockinper2Val = Double.Parse(LockInPertext2.Text); // in kHz
            double lockinfreq2Val = 1 / lockinper2Val * 1000; // in microseconds
            LockInFreqtext2.Text = lockinfreq2Val.ToString();
        }
        private void LockInFreqtext2B_TextChanged(object sender, EventArgs e)
        {
            double lockinfreq2BVal = Double.Parse(LockInFreqtext2B.Text); // in kHz
            double lockinper2BVal = 1 / lockinfreq2BVal * 1000; // in microseconds
            LockInPertext2B.Text = lockinper2BVal.ToString();
        }
        private void LockInPertext2B_TextChanged(object sender, EventArgs e)
        {
            double lockinper2BVal = Double.Parse(LockInPertext2B.Text); // in kHz
            double lockinfreq2BVal = 1 / lockinper2BVal * 1000; // in microseconds
            LockInFreqtext2B.Text = lockinfreq2BVal.ToString();
        }

        private void pulsePeriodText_TextChanged(object sender, EventArgs e)
        {
            double pulsePeriodVal = Double.Parse(pulsePeriodText.Text); // in microseconds
            double pulseFreqVal = 1/pulsePeriodVal*1000; // in kHz
            pulseFreqLabel.Text = pulseFreqVal.ToString();
            //theCorrelator.PulseClkDiv = (int)(Math.Round(theCorrelator.ok.P * pulsePeriodVal, 0));
            //theCorrelator.updateCorrPulseClkDivLive();
        }
        ///////////////////////////////////////////////////////////

        private void updateAllSignalsButton_Click(object sender, EventArgs e)
        {
            theCorrelator.PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(pulsePeriodText.Text), 0));

            theCorrelator.onTimeOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1OnTimeText.Text), 0));
            theCorrelator.onTimeOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2OnTimeText.Text), 0));
            theCorrelator.delayOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1DelayText.Text), 0));
            theCorrelator.delayOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2DelayText.Text), 0));

            theCorrelator.onTimeIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1OnTimeText.Text), 0));
            theCorrelator.onTimeIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2OnTimeText.Text), 0));
            theCorrelator.delayIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1DelayText.Text), 0));
            theCorrelator.delayIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2DelayText.Text), 0));

            theCorrelator.updateAllSignalsLive();
            /*
            try
            {
                
                //plot
                NationalInstruments.WaveformTiming timing = new NationalInstruments.WaveformTiming;
                timing.
                DateTime[] ch1 = { DateTime.FromBinary(0), DateTime.FromBinary(long.Parse(out1DelayText.Text)) };
                NationalInstruments.WaveformTiming timing = new NationalInstruments.WaveformTiming;
                NationalInstruments.DigitalWaveform dwave = new NationalInstruments.DigitalWaveform(2, 1);
                dwave.Timing = 
                PulseProgrammerTimingDiagram.PlotWaveform(dwave);

                digitalSignalPlot1.TransitionLocation
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }  
             * */
        }

        private void syncSrcSw_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (syncSrcSw.Value) { theCorrelator.syncSrcChoose = true; }
            else { theCorrelator.syncSrcChoose = false; }

            theCorrelator.updateSyncSourceLive();
        }

        private void pulsedDutyText_TextChanged(object sender, EventArgs e)
        {

        }

        private void label142_Click(object sender, EventArgs e)
        {

        }

        private void Recool_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void label99_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        //////////////////////////////////////////////////
        ////////////Fast output ch2 //////////////////
        private void Indfo2_CheckedChanged(object sender, EventArgs e)
        {
            if (Indfo2.Checked)
            {
                out2OnTimeText.Enabled = true;
                out2DelayText.Enabled = true;
                out2OnTimeText.ForeColor = Color.Black;
                out2DelayText.ForeColor = Color.Black;
            }
        }

        private void c1fo2_CheckedChanged(object sender, EventArgs e)
        {
            if (c1fo2.Checked)
            {
                out2OnTimeText.Text = out1OnTimeText.Text;
                out2DelayText.Text = out1DelayText.Text;

                out2OnTimeText.Enabled = false;
                out2DelayText.Enabled = false;
                out2OnTimeText.ForeColor = Color.Gray;
                out2DelayText.ForeColor = Color.Gray;
            }
        }
        private void notc1fo2_CheckedChanged(object sender, EventArgs e)
        {
            if (notc1fo2.Checked)
            {

                double vaux2 = double.Parse(out1DelayText.Text) + double.Parse(out1OnTimeText.Text);
                double vaux1 = double.Parse(pulsePeriodText.Text)-vaux2;

                out2OnTimeText.Text = vaux1.ToString();
                out2DelayText.Text = vaux2.ToString();

                out2OnTimeText.Enabled = false;
                out2DelayText.Enabled = false;
                out2OnTimeText.ForeColor = Color.Gray;
                out2DelayText.ForeColor = Color.Gray;
            }
        }
        ///////////////////////////////////////////////////////////////

        //////////////////////////////////////////////////
        ////////////Fast input ch1 //////////////////
        private void Indfi1_CheckedChanged(object sender, EventArgs e)
        {
            if (Indfi1.Checked)
            {
                in1OnTimeText.Enabled = true;
                in1DelayText.Enabled = true;
                in1OnTimeText.ForeColor = Color.Black;
                in1DelayText.ForeColor = Color.Black;
            }
        }
        private void c1fi1_CheckedChanged(object sender, EventArgs e)
        {
            if (c1fi1.Checked)
            {
                in1OnTimeText.Text = out1OnTimeText.Text;
                in1DelayText.Text = out1DelayText.Text;

                in1OnTimeText.Enabled = false;
                in1DelayText.Enabled = false;
                in1OnTimeText.ForeColor = Color.Gray;
                in1DelayText.ForeColor = Color.Gray;
            }
        }
        private void c2fi1_CheckedChanged(object sender, EventArgs e)
        {
            if (c2fi1.Checked)
            {
                in1OnTimeText.Text = out2OnTimeText.Text;
                in1DelayText.Text = out2DelayText.Text;

                in1OnTimeText.Enabled = false;
                in1DelayText.Enabled = false;
                in1OnTimeText.ForeColor = Color.Gray;
                in1DelayText.ForeColor = Color.Gray;
            }
        }

        //////////////////////////////////////////////////
        ////////////Fast input ch2 //////////////////
        private void Indfi2_CheckedChanged(object sender, EventArgs e)
        {
            if (Indfi2.Checked)
            {
                in2OnTimeText.Enabled = true;
                in2DelayText.Enabled = true;
                in2OnTimeText.ForeColor = Color.Black;
                in2DelayText.ForeColor = Color.Black;
            }
        }
        private void c1fi2_CheckedChanged(object sender, EventArgs e)
        {
            if (c1fi2.Checked)
            {
                in2OnTimeText.Text = out1OnTimeText.Text;
                in2DelayText.Text = out1DelayText.Text;

                in2OnTimeText.Enabled = false;
                in2DelayText.Enabled = false;
                in2OnTimeText.ForeColor = Color.Gray;
                in2DelayText.ForeColor = Color.Gray;
            }
        }
        private void c2fi2_CheckedChanged(object sender, EventArgs e)
        {
            if (c2fi2.Checked)
            {
                in2OnTimeText.Text = out2OnTimeText.Text;
                in2DelayText.Text = out2DelayText.Text;

                in2OnTimeText.Enabled = false;
                in2DelayText.Enabled = false;
                in2OnTimeText.ForeColor = Color.Gray;
                in2DelayText.ForeColor = Color.Gray;
            }
        }


        //////////////////////////////////////////////////
        ////////////Slow output ch2 //////////////////
        private void Indso2_CheckedChanged(object sender, EventArgs e)
        {
            if (Indso2.Checked)
            {
                slow_out2OnTimeText.Enabled = true;
                slow_out2DelayText.Enabled = true;
                slow_out2OnTimeText.ForeColor = Color.Black;
                slow_out2DelayText.ForeColor = Color.Black;
            }
        }

        private void c1so2_CheckedChanged(object sender, EventArgs e)
        {
            if (c1so2.Checked)
            {
                slow_out2OnTimeText.Text = out1OnTimeText.Text;
                slow_out2DelayText.Text = out1DelayText.Text;

                slow_out2OnTimeText.Enabled = false;
                slow_out2DelayText.Enabled = false;
                slow_out2OnTimeText.ForeColor = Color.Gray;
                slow_out2DelayText.ForeColor = Color.Gray;
            }
        }
        private void notc1so2_CheckedChanged(object sender, EventArgs e)
        {
            if (notc1so2.Checked)
            {

                double vaux2 = double.Parse(slow_out1DelayText.Text) + double.Parse(slow_out1OnTimeText.Text);
                double vaux1 = double.Parse(slow_pulsePeriodText.Text) - vaux2;

                slow_out2OnTimeText.Text = vaux1.ToString();
                slow_out2DelayText.Text = vaux2.ToString();

                slow_out2OnTimeText.Enabled = false;
                slow_out2DelayText.Enabled = false;
                slow_out2OnTimeText.ForeColor = Color.Gray;
                slow_out2DelayText.ForeColor = Color.Gray;
            }
        }
        ///////////////////////////////////////////////////////////////

        //////////////////////////////////////////////////
        ////////////Fast input ch1 //////////////////
        private void Indsi1_CheckedChanged(object sender, EventArgs e)
        {
            if (Indsi1.Checked)
            {
                slow_in1OnTimeText.Enabled = true;
                slow_in1DelayText.Enabled = true;
                slow_in1OnTimeText.ForeColor = Color.Black;
                slow_in1DelayText.ForeColor = Color.Black;
            }
        }
        private void c1si1_CheckedChanged(object sender, EventArgs e)
        {
            if (c1si1.Checked)
            {
                slow_in1OnTimeText.Text = slow_out1OnTimeText.Text;
                slow_in1DelayText.Text = slow_out1DelayText.Text;

                slow_in1OnTimeText.Enabled = false;
                slow_in1DelayText.Enabled = false;
                slow_in1OnTimeText.ForeColor = Color.Gray;
                slow_in1DelayText.ForeColor = Color.Gray;
            }
        }
        private void c2si1_CheckedChanged(object sender, EventArgs e)
        {
            if (c2si1.Checked)
            {
                slow_in1OnTimeText.Text = slow_out2OnTimeText.Text;
                slow_in1DelayText.Text = slow_out2DelayText.Text;

                slow_in1OnTimeText.Enabled = false;
                slow_in1DelayText.Enabled = false;
                slow_in1OnTimeText.ForeColor = Color.Gray;
                slow_in1DelayText.ForeColor = Color.Gray;
            }
        }

        //////////////////////////////////////////////////
        ////////////Fast input ch2 //////////////////
        private void Indsi2_CheckedChanged(object sender, EventArgs e)
        {
            if (Indsi2.Checked)
            {
                slow_in2OnTimeText.Enabled = true;
                slow_in2DelayText.Enabled = true;
                slow_in2OnTimeText.ForeColor = Color.Black;
                slow_in2DelayText.ForeColor = Color.Black;
            }
        }
        private void c1si2_CheckedChanged(object sender, EventArgs e)
        {
            if (c1si2.Checked)
            {
                slow_in2OnTimeText.Text = slow_out1OnTimeText.Text;
                slow_in2DelayText.Text = slow_out1DelayText.Text;

                slow_in2OnTimeText.Enabled = false;
                slow_in2DelayText.Enabled = false;
                slow_in2OnTimeText.ForeColor = Color.Gray;
                slow_in2DelayText.ForeColor = Color.Gray;
            }
        }
        private void c2si2_CheckedChanged(object sender, EventArgs e)
        {
            if (c2si2.Checked)
            {
                slow_in2OnTimeText.Text = slow_out2OnTimeText.Text;
                slow_in2DelayText.Text = slow_out2DelayText.Text;

                slow_in2OnTimeText.Enabled = false;
                slow_in2DelayText.Enabled = false;
                slow_in2OnTimeText.ForeColor = Color.Gray;
                slow_in2DelayText.ForeColor = Color.Gray;
            }
        }

        private void out1_Changed(object sender, EventArgs e)
        {
            if (c1fo2.Checked)
            {
                out2OnTimeText.Text = out1OnTimeText.Text;
                out2DelayText.Text = out1DelayText.Text;
            }
            else if (notc1fo2.Checked)
            {
                double vaux2 = double.Parse(out1DelayText.Text) + double.Parse(out1OnTimeText.Text);
                double vaux1 = double.Parse(pulsePeriodText.Text) - vaux2;

                out2OnTimeText.Text = vaux1.ToString();
                out2DelayText.Text = vaux2.ToString();
            }

            if (c1fi1.Checked)
            {
                in1OnTimeText.Text = out1OnTimeText.Text;
                in1DelayText.Text = out1DelayText.Text;
            }
            if (c1fi2.Checked)
            {
                in2OnTimeText.Text = out1OnTimeText.Text;
                in2DelayText.Text = out1DelayText.Text;
            }
        }

        private void out2_Changed(object sender, EventArgs e)
        {
            if (c2fi1.Checked)
            {
                in1OnTimeText.Text = out2OnTimeText.Text;
                in1DelayText.Text = out2DelayText.Text;
            }
            if (c2fi2.Checked)
            {
                in2OnTimeText.Text = out2OnTimeText.Text;
                in2DelayText.Text = out2DelayText.Text;
            }
        }

        private void pulsePeriodText_TextChanged_1(object sender, EventArgs e)
        {
            if (notc1fo2.Checked)
            {
                double vaux2 = double.Parse(out1DelayText.Text) + double.Parse(out1OnTimeText.Text);
                double vaux1 = double.Parse(pulsePeriodText.Text) - vaux2;

                out2OnTimeText.Text = vaux1.ToString();
                out2DelayText.Text = vaux2.ToString();
            }
        }

        private void slow_pulsePeriodText_TextChanged(object sender, EventArgs e)
        {
            if (notc1so2.Checked)
            {
                double vaux2 = double.Parse(slow_out1DelayText.Text) + double.Parse(slow_out1OnTimeText.Text);
                double vaux1 = double.Parse(slow_pulsePeriodText.Text) - vaux2;

                slow_out2OnTimeText.Text = vaux1.ToString();
                slow_out2DelayText.Text = vaux2.ToString();
            }
        }

        private void slow_out1_Changed(object sender, EventArgs e)
        {
            if (c1so2.Checked)
            {
                slow_out2OnTimeText.Text = slow_out1OnTimeText.Text;
                slow_out2DelayText.Text = slow_out1DelayText.Text;
            }
            else if (notc1so2.Checked)
            {
                double vaux2 = double.Parse(slow_out1DelayText.Text) + double.Parse(slow_out1OnTimeText.Text);
                double vaux1 = double.Parse(slow_pulsePeriodText.Text) - vaux2;

                slow_out2OnTimeText.Text = vaux1.ToString();
                slow_out2DelayText.Text = vaux2.ToString();
            }

            if (c1si1.Checked)
            {
                slow_in1OnTimeText.Text = slow_out1OnTimeText.Text;
                slow_in1DelayText.Text = slow_out1DelayText.Text;
            }
            if (c1si2.Checked)
            {
                slow_in2OnTimeText.Text = slow_out1OnTimeText.Text;
                slow_in2DelayText.Text = slow_out1DelayText.Text;
            }
        }

        private void slow_out2_Changed(object sender, EventArgs e)
        {
            if (c2si1.Checked)
            {
                slow_in1OnTimeText.Text = slow_out2OnTimeText.Text;
                slow_in1DelayText.Text = slow_out2DelayText.Text;
            }
            if (c2si2.Checked)
            {
                slow_in2OnTimeText.Text = slow_out2OnTimeText.Text;
                slow_in2DelayText.Text = slow_out2DelayText.Text;
            }
        }

        
        //
        // CAMERA FORM THREAD
        //
        /*
        public void CameraFormThreadExecute()
        {
            
            try
            {
                this.Invoke(new MyDelegate(CameraFormThreadFrmCallback));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            while (true) { }
        }
        public void CameraFormThreadFrmCallback()
        {
            CameraForm.Show(this);
        }
         */
            
    }

    }

           
    

