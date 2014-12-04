using System;
using System.Collections;
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
using NationalInstruments.VisaNS;
using System.Web.UI.WebControls;



namespace ArrayDACControl
{
    public partial class Form1 : Form
    {
        public static Form1 Self;

        //global variables for Rigol Tab
        ArrayList importedWaveforms = new ArrayList(); //ArrayList to store importedWaveforms
        int index = 0; //each waveform is assigned an index number

        DACController DAC;
        NICardController Dev4AO0, Dev4AO1, Dev4AO2, Dev4AO3, Dev4AO4, Dev4AO5, Dev4AO6, Dev4AO7, Dev7AO0, Dev7AO2, Dev7AO6, Dev7AO7;
        NICardController Dev2DO0, Dev2DO1, Dev2DO2, Dev2DO3, Dev2DO4, Dev2DO5, Dev2DO6, Dev2DO7;
        NICardController Dev3AI2;
        GPIB gpibdevice;
        Andor Camera;
        Form2 CameraForm;
        Correlator theCorrelator;
        
        Stopwatch stopwatch;
        public int ncorrbins;   
        const int nfastchOut = 5;
        const int nslowchOut = 5;
        const int nfastchIn = 3;
        const int nslowchIn = 3;
        PulseSequencerChannel[] fastOutCh;
        PulseSequencerChannel[] slowOutCh;
        PulseSequencerChannel[] fastInCh;
        PulseSequencerChannel[] slowInCh;
        Color[] chcolorarray = new Color[] { Color.Blue, Color.Red, Color.Green, Color.Orange, Color.Purple, Color.Brown, Color.Cyan, Color.Lime };

        double pulsePeriodVal;
        double slow_pulsePeriodVal;

        public int counterMu = 0;
        public int counterAmp = 0;
        public double[][] corrampCh1history;
        public double[][] corrampCh2history;
        public int historyCounter = 0;
        double binningPhase;

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
        ThreadHelperClass LatticePositionThreadHelper;
        public ThreadHelperClass ExperimentalSequencerThreadHelper;
        ThreadHelperClass InterlockedScan1ThreadHelper, InterlockedScan2ThreadHelper;
        //delegate methods for cross-thread calls
        delegate void MyDelegate();
        delegate void MyDelegateThreadHelper(ThreadHelperClass theThreadHelper);
        delegate void MyDelegateLabelUpdate(string theString, System.Windows.Forms.Label theLabel);
        delegate void MyDelegateThreadHelperComboBox(ThreadHelperClass theThreadHelper, ComboBox theComboBox);

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

        private System.Windows.Forms.Label[,] DCindicators;

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
            Self = this;

            fastOutCh = new PulseSequencerChannel[nfastchOut];
            slowOutCh = new PulseSequencerChannel[nslowchOut];
            fastInCh = new PulseSequencerChannel[nfastchIn];
            slowInCh = new PulseSequencerChannel[nslowchIn];

            for (int i = 0; i < fastOutCh.Length; i++)
                fastOutCh[i] = new PulseSequencerChannel();
            for (int i = 0; i < slowOutCh.Length; i++)
                slowOutCh[i] = new PulseSequencerChannel();
            for (int i = 0; i < fastInCh.Length; i++)
                fastInCh[i] = new PulseSequencerChannel();
            for (int i = 0; i < slowInCh.Length; i++)
                slowInCh[i] = new PulseSequencerChannel();

            DCindicators = new System.Windows.Forms.Label[2, DCrows];
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
            initializeSequencerForm();

            DAC = new DACController();
            // Raman VVA
            Dev4AO0 = new NICardController();
            Dev4AO0.InitAnalogOutput("Dev4/ao0", 0, 10);
            // Repumper Color
            Dev4AO1 = new NICardController();
            Dev4AO1.InitAnalogOutput("Dev4/ao1", 0, 10); //Repumper HV amp input saturates at 7V
            // 399 Error Offset
            Dev4AO2 = new NICardController();
            Dev4AO2.InitAnalogOutput("Dev4/ao2", 0, 10);
            // Transfer Cavity Piezo
            Dev4AO3 = new NICardController();
            Dev4AO3.InitAnalogOutput("Dev4/ao3", 0, 10);
            // Recapture Power Control
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
            // 935 Shutter
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
            LatticePositionThreadHelper = new ThreadHelperClass("LatticePosition");
            ExperimentalSequencerThreadHelper = new ThreadHelperClass("ExperimentalSequencer");
            InterlockedScan1ThreadHelper = new ThreadHelperClass("InterlockedScan1");
            InterlockedScan2ThreadHelper = new ThreadHelperClass("InterlockedScan2");

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
                DCsliders [i].AbsMin = -20;
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
            this.RecapturePowerSlider.SliderAdjusted += this.RecapturePowerSliderOut;
            this.ErrorOffset399Slider.SliderAdjusted += this.ErrorOffset399SliderOut;
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

        private void initializeSequencerForm()
        {
            //positions of the tables of channels:
            int fastoutxpos = 20;
            int fastoutypos = 275+400;
            int slowoutxpos = 840;
            int slowoutypos = fastoutypos;
            int fastinxpos = fastoutxpos;
            int slowinxpos = slowoutxpos;
            int fastinypos = fastoutypos + fastInCh.Length * 47 + 60;
            int slowinypos = fastinypos;

            pulsePeriodVal = double.Parse(pulsePeriodText.Text);
            slow_pulsePeriodVal = double.Parse(pulsePeriodText.Text);
            //pulsePeriodText_TextChanged();

            // FAST OUTPUT CHANNELS:
            for (int i = 0; i < fastOutCh.Length; i++)
            {
                fastOutCh[i].paramsEntered += this.refreshChannels;
                fastOutCh[i].optionChanged += this.refreshChannels;
                fastOutCh[i].Size = new System.Drawing.Size(800, 47);
                //DCsliders[i].TabIndex = 90 + i;
                fastOutCh[i].Location = new System.Drawing.Point(fastoutxpos, fastoutypos - (47) * ((fastOutCh.Length - i - 1) - 1));
                fastOutCh[i].ChannelLabel = String.Format("ch {0}", i+1);
                fastOutCh[i].ChannelColor = chcolorarray[i];
                fastOutCh[i].Param1Label = String.Format("sub-period");
                fastOutCh[i].Param2Label = String.Format("ON duration");
                fastOutCh[i].Param3Label = String.Format("delay");
                if (i == 0)
                {
                    fastOutCh[i].Opt2Label = String.Format("ON");
                    fastOutCh[i].Opt3Label = String.Format("OFF");
                }
                else if (i == 1)
                {
                    fastOutCh[i].Opt2Label = String.Format("ch1");
                    fastOutCh[i].Opt3Label = String.Format("OFF");
                }
                else
                {
                    fastOutCh[i].Opt2Label = String.Format("ch1");
                    fastOutCh[i].Opt3Label = String.Format("ch2");
                }
                fastOutCh[i].opt1Value = true;
                fastOutCh[i].opt2Value = false;
                fastOutCh[i].opt3Value = false;
                fastOutCh[i].ExpPeriodValue = pulsePeriodVal;
                this.Controls.Add(fastOutCh[i]);
                this.CorrelatorTab.Controls.Add(this.fastOutCh[i]);


            }

            // FAST INPUT CHANNELS:
            label146.Location = new System.Drawing.Point(14, fastinypos-80);
            label184.Location = new System.Drawing.Point(302, fastinypos - 80);
            for (int i = 0; i < fastInCh.Length; i++)
            {
                fastInCh[i].paramsEntered += this.refreshChannels;
                fastInCh[i].optionChanged += this.refreshChannels;
                fastInCh[i].Size = new System.Drawing.Size(800, 47);
                //DCsliders[i].TabIndex = 90 + i;
                fastInCh[i].Location = new System.Drawing.Point(fastinxpos, fastinypos - (47) * ((fastInCh.Length - i - 1) - 1));
                fastInCh[i].ChannelLabel = String.Format("ch {0}", i+1);
                fastInCh[i].ChannelColor = chcolorarray[i];
                fastInCh[i].Param1Label = String.Format("sub-period");
                fastInCh[i].Param2Label = String.Format("ON duration");
                fastInCh[i].Param3Label = String.Format("delay");
                fastInCh[i].Opt2Label = String.Format("out1");
                fastInCh[i].Opt3Label = String.Format("out2");
                fastInCh[i].opt1Value = true;
                fastInCh[i].opt2Value = false;
                fastInCh[i].opt3Value = false;
                fastInCh[i].ExpPeriodValue = pulsePeriodVal;
                this.Controls.Add(fastInCh[i]);
                this.CorrelatorTab.Controls.Add(this.fastInCh[i]);
            }

            //SLOW OUTPUT CHANNELS
            for (int i = 0; i < slowOutCh.Length; i++)
            {
                slowOutCh[i].paramsEntered += this.refreshChannels;
                slowOutCh[i].optionChanged += this.refreshChannels;
                slowOutCh[i].Size = new System.Drawing.Size(800, 47);
                //DCsliders[i].TabIndex = 90 + i;
                slowOutCh[i].Location = new System.Drawing.Point(slowoutxpos, slowoutypos - (47) * ((slowOutCh.Length - i - 1) - 1));
                slowOutCh[i].ChannelLabel = String.Format("ch {0}", i+1);
                slowOutCh[i].ChannelColor = chcolorarray[i];
                slowOutCh[i].Param1Label = String.Format("sub-period");
                slowOutCh[i].Param2Label = String.Format("ON duration");
                slowOutCh[i].Param3Label = String.Format("delay");
                if (i == 0)
                {
                    slowOutCh[i].Opt2Label = String.Format("ON");
                    slowOutCh[i].Opt3Label = String.Format("OFF");
                }
                else if (i == 1)
                {
                    slowOutCh[i].Opt2Label = String.Format("ch1");
                    slowOutCh[i].Opt3Label = String.Format("OFF");
                }
                else
                {
                    slowOutCh[i].Opt2Label = String.Format("ch1");
                    slowOutCh[i].Opt3Label = String.Format("ch2");
                }
                slowOutCh[i].opt1Value = true;
                slowOutCh[i].opt2Value = false;
                slowOutCh[i].opt3Value = false;
                slowOutCh[i].ExpPeriodValue = slow_pulsePeriodVal;
                this.Controls.Add(slowOutCh[i]);
                this.CorrelatorTab.Controls.Add(this.slowOutCh[i]);
            }

            //SLOW INPUT CHANNELS
            label110.Location = new System.Drawing.Point(879, slowinypos-80);
            for (int i = 0; i < slowInCh.Length; i++)
            {
                slowInCh[i].paramsEntered += this.refreshChannels;
                slowInCh[i].optionChanged += this.refreshChannels;
                slowInCh[i].Size = new System.Drawing.Size(800, 47);
                //DCsliders[i].TabIndex = 90 + i;
                slowInCh[i].Location = new System.Drawing.Point(slowinxpos, slowinypos - (47) * ((slowInCh.Length - i - 1) - 1));
                slowInCh[i].ChannelLabel = String.Format("ch {0}", i+1);
                slowInCh[i].ChannelColor = chcolorarray[i];
                slowInCh[i].Param1Label = String.Format("sub-period");
                slowInCh[i].Param2Label = String.Format("ON duration");
                slowInCh[i].Param3Label = String.Format("delay");
                slowInCh[i].Opt2Label = String.Format("out1");
                slowInCh[i].Opt3Label = String.Format("out2");
                slowInCh[i].opt1Value = true;
                slowInCh[i].opt2Value = false;
                slowInCh[i].opt3Value = false;
                slowInCh[i].ExpPeriodValue = slow_pulsePeriodVal;
                this.Controls.Add(slowInCh[i]);
                this.CorrelatorTab.Controls.Add(this.slowInCh[i]);
            }


        }

        public void refreshChannels(object sender, EventArgs e)
        {
            refreshChannelsHelper();
        }
        private void refreshChannelsHelper()
        {

            // FAST OUTPUT CHANNELS:
            for (int i = 0; i < fastOutCh.Length; i++)
            {
                fastOutCh[i].ExpPeriodValue = pulsePeriodVal;

                if (i == 0)
                {
                    //always on option for first channel
                    fastOutCh[i].Param1opt2Value = pulsePeriodVal;
                    fastOutCh[i].Param2opt2Value = pulsePeriodVal;
                    fastOutCh[i].Param3opt2Value = 0;
                    //always off option for first channel
                    fastOutCh[i].Param1opt3Value = pulsePeriodVal;
                    fastOutCh[i].Param2opt3Value = 0;
                    fastOutCh[i].Param3opt3Value = 0;
                }
                else if(i==1)
                {
                    //ch1 option for second channel
                    fastOutCh[i].Param1opt2Value = fastOutCh[0].Param1Value;
                    fastOutCh[i].Param2opt2Value = fastOutCh[0].Param2Value;
                    fastOutCh[i].Param3opt2Value = fastOutCh[0].Param3Value;
                    //always off option for second channel
                    fastOutCh[i].Param1opt3Value = pulsePeriodVal;
                    fastOutCh[i].Param2opt3Value = 0;
                    fastOutCh[i].Param3opt3Value = 0;
                }
                else
                {
                    //ch1 option
                    fastOutCh[i].Param1opt2Value = fastOutCh[0].Param1Value;
                    fastOutCh[i].Param2opt2Value = fastOutCh[0].Param2Value;
                    fastOutCh[i].Param3opt2Value = fastOutCh[0].Param3Value;
                    //ch2 option
                    fastOutCh[i].Param1opt3Value = fastOutCh[1].Param1Value;
                    fastOutCh[i].Param2opt3Value = fastOutCh[1].Param2Value;
                    fastOutCh[i].Param3opt3Value = fastOutCh[1].Param3Value;
                }
            }

            // FAST INPUT CHANNELS:
            for (int i = 0; i < fastInCh.Length; i++)
            {
                fastInCh[i].ExpPeriodValue = pulsePeriodVal;

                    //ch1 option
                    fastInCh[i].Param1opt2Value = fastOutCh[0].Param1Value;
                    fastInCh[i].Param2opt2Value = fastOutCh[0].Param2Value;
                    fastInCh[i].Param3opt2Value = fastOutCh[0].Param3Value;
                    //ch2 option
                    fastInCh[i].Param1opt3Value = fastOutCh[1].Param1Value;
                    fastInCh[i].Param2opt3Value = fastOutCh[1].Param2Value;
                    fastInCh[i].Param3opt3Value = fastOutCh[1].Param3Value;
            }

            //SLOW OUTPUT CHANNELS
            for (int i = 0; i < slowOutCh.Length; i++)
            {
                slowOutCh[i].ExpPeriodValue = slow_pulsePeriodVal;

                if (i == 0)
                {
                    //always on option for first channel
                    slowOutCh[i].Param1opt2Value = slow_pulsePeriodVal;
                    slowOutCh[i].Param2opt2Value = slow_pulsePeriodVal;
                    slowOutCh[i].Param3opt2Value = 0;
                    //always off option for first channel
                    slowOutCh[i].Param1opt3Value = slow_pulsePeriodVal;
                    slowOutCh[i].Param2opt3Value = 0;
                    slowOutCh[i].Param3opt3Value = 0;
                }
                else if(i==1)
                {
                    //ch1 option for second channel
                    slowOutCh[i].Param1opt2Value = slowOutCh[0].Param1Value;
                    slowOutCh[i].Param2opt2Value = slowOutCh[0].Param2Value;
                    slowOutCh[i].Param3opt2Value = slowOutCh[0].Param3Value;
                    //always off option for second channel
                    slowOutCh[i].Param1opt3Value = slow_pulsePeriodVal;
                    slowOutCh[i].Param2opt3Value = 0;
                    slowOutCh[i].Param3opt3Value = 0;
                }
                else
                {
                    //ch1 option
                    slowOutCh[i].Param1opt2Value = slowOutCh[0].Param1Value;
                    slowOutCh[i].Param2opt2Value = slowOutCh[0].Param2Value;
                    slowOutCh[i].Param3opt2Value = slowOutCh[0].Param3Value;
                    //ch2 option
                    slowOutCh[i].Param1opt3Value = slowOutCh[1].Param1Value;
                    slowOutCh[i].Param2opt3Value = slowOutCh[1].Param2Value;
                    slowOutCh[i].Param3opt3Value = slowOutCh[1].Param3Value;
                }
            }

            //SLOW INPUT CHANNELS
            for (int i = 0; i < slowInCh.Length; i++)
            {
                slowInCh[i].ExpPeriodValue = slow_pulsePeriodVal;

                    //ch1 option
                slowInCh[i].Param1opt2Value = slowOutCh[0].Param1Value;
                slowInCh[i].Param2opt2Value = slowOutCh[0].Param2Value;
                slowInCh[i].Param3opt2Value = slowOutCh[0].Param3Value;
                    //ch2 option
                slowInCh[i].Param1opt3Value = slowOutCh[1].Param1Value;
                slowInCh[i].Param2opt3Value = slowOutCh[1].Param2Value;
                slowInCh[i].Param3opt3Value = slowOutCh[1].Param3Value;
            }
        }

        private void UpdateAll()
        {
            compensationAdjustedHelper();
            RepumperSliderOutHelper();
            BxSliderOutHelper();
            TickleSliderOutHelper();
            RecapturePowerSliderOutHelper();
            //Dev4AO2.OutputAnalogValue((double)(TransferCavity.Value - CurrentFeedforward370Offset.Value) * CurrentFeedforward370Gain.Value / TCcalib);
            Dev4AO2.OutputAnalogValue(ErrorOffset399Slider.Value);
            Dev4AO3.OutputAnalogValue((double)TransferCavity.Value / TCcalib);
            Dev4AO4.OutputAnalogValue((double)RecapturePowerSlider.Value);
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
                DCindicators[0, i].Text = String.Format("{0:F2}", -DX / 2 + DCvalues[i] - DCvaluesDx[i] + DCvaluesLeft[i] + QuadTiltRatioSlider.Value * QuadrupoleTilt.Value);
                DCindicators[1, i].Text = String.Format("{0:F2}", +DX / 2 + DCvalues[i] + DCvaluesDx[i] + DCvaluesRight[i] - QuadTiltRatioSlider.Value * QuadrupoleTilt.Value);
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
                    //CurrentFeedforward370Offset.Value = TransferCavity.Value;
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
        private void SaveConfigurationFull(string path, string extraname)
        {
            try
            {

                //check if folder exists, if not create it
                if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }

                System.IO.StreamWriter tw = new System.IO.StreamWriter(path + extraname + DateTime.Now.ToString("yyyyMMdd") + " " + DateTime.Now.ToString("hhmmss") + ".txt");

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
                //tw.WriteLine("CurrentFeedforward370Offset" + "\t" + CurrentFeedforward370Offset.Value);
                //tw.WriteLine("CurrentFeedforward370Gain" + "\t" + CurrentFeedforward370Gain.Value);
                tw.WriteLine("SideBeam370Power" + "\t" + SideBeam370Power.Value);
                tw.WriteLine("CavityCoolingPowerControl" + "\t" + CavityCoolingPowerControl.Value);
                tw.WriteLine("RecapturePowerSlider" + "\t" + RecapturePowerSlider.Value);
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
                tw.WriteLine("LatticePositionDC1TextBox" + "\t" + LatticePositionDC1TextBox.Text);
                tw.WriteLine("LatticePositionDC2TextBox" + "\t" + LatticePositionDC2TextBox.Text);
                tw.WriteLine("LatticePositionAmplitudeText" + "\t" + LatticePositionAmplitudeText.Text);
                tw.WriteLine("LatticePositionNumAveText" + "\t" + LatticePositionNumAveText.Text);
                tw.WriteLine("LatticePositionRampArrayValue" + "\t" + LatticePositionRampArrayValue.Text);
                

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

                //////////////////////

                //In order of appearance, top left, to bottom right
                tw.WriteLine("Sync period" + "\t" + LockInPertext1.Text);
                tw.WriteLine("Fast Overall Period" + "\t" + pulsePeriodVal.ToString());
                tw.WriteLine("Slow Overall Period" + "\t" + slow_pulsePeriodVal.ToString());

                for (int i = 0; i < nfastchOut; i++)
                {
                    tw.WriteLine("Fast Out \t" + i.ToString() + "\t name \t" + fastOutCh[i].Name);
                    tw.WriteLine("Fast Out \t" + i.ToString() + "\t OptInd \t" + fastOutCh[i].opt1Value.ToString());
                    tw.WriteLine("Fast Out \t" + i.ToString() + "\t Opt2 \t" + fastOutCh[i].opt2Value.ToString());
                    tw.WriteLine("Fast Out \t" + i.ToString() + "\t Opt3 \t" + fastOutCh[i].opt3Value.ToString());
                    tw.WriteLine("Fast Out \t" + i.ToString() + "\t param1 \t" + fastOutCh[i].Param1Value.ToString());
                    tw.WriteLine("Fast Out \t" + i.ToString() + "\t param2 \t" + fastOutCh[i].Param2Value.ToString());
                    tw.WriteLine("Fast Out \t" + i.ToString() + "\t param3 \t" + fastOutCh[i].Param3Value.ToString());

                }
                for (int i = 0; i < nfastchIn; i++)
                {
                    tw.WriteLine("Fast In \t" + i.ToString() + "\t name \t" + fastInCh[i].Name);
                    tw.WriteLine("Fast In \t" + i.ToString() + "\t OptInd \t" + fastInCh[i].opt1Value.ToString());
                    tw.WriteLine("Fast In \t" + i.ToString() + "\t Opt2 \t" + fastInCh[i].opt2Value.ToString());
                    tw.WriteLine("Fast In \t" + i.ToString() + "\t Opt3 \t" + fastInCh[i].opt3Value.ToString());
                    tw.WriteLine("Fast In \t" + i.ToString() + "\t param1 \t" + fastInCh[i].Param1Value.ToString());
                    tw.WriteLine("Fast In \t" + i.ToString() + "\t param2 \t" + fastInCh[i].Param2Value.ToString());
                    tw.WriteLine("Fast In \t" + i.ToString() + "\t param3 \t" + fastInCh[i].Param3Value.ToString());
                }
                for (int i = 0; i < nslowchOut; i++)
                {
                    tw.WriteLine("Slow Out \t" + i.ToString() + "\t name \t" + slowOutCh[i].Name);
                    tw.WriteLine("Slow Out \t" + i.ToString() + "\t OptInd \t" + slowOutCh[i].opt1Value.ToString());
                    tw.WriteLine("Slow Out \t" + i.ToString() + "\t Opt2 \t" + slowOutCh[i].opt2Value.ToString());
                    tw.WriteLine("Slow Out \t" + i.ToString() + "\t Opt3 \t" + slowOutCh[i].opt3Value.ToString());
                    tw.WriteLine("Slow Out \t" + i.ToString() + "\t param1 \t" + slowOutCh[i].Param1Value.ToString());
                    tw.WriteLine("Slow Out \t" + i.ToString() + "\t param2 \t" + slowOutCh[i].Param2Value.ToString());
                    tw.WriteLine("Slow Out \t" + i.ToString() + "\t param3 \t" + slowOutCh[i].Param3Value.ToString());
                }
                for (int i = 0; i < nslowchIn; i++)
                {
                    tw.WriteLine("Slow In \t" + i.ToString() + "\t name \t" + slowInCh[i].Name);
                    tw.WriteLine("Slow In \t" + i.ToString() + "\t OptInd \t" + slowInCh[i].opt1Value.ToString());
                    tw.WriteLine("Slow In \t" + i.ToString() + "\t Opt2 \t" + slowInCh[i].opt2Value.ToString());
                    tw.WriteLine("Slow In \t" + i.ToString() + "\t Opt3 \t" + slowInCh[i].opt3Value.ToString());
                    tw.WriteLine("Slow In \t" + i.ToString() + "\t param1 \t" + slowInCh[i].Param1Value.ToString());
                    tw.WriteLine("Slow In \t" + i.ToString() + "\t param2 \t" + slowInCh[i].Param2Value.ToString());
                    tw.WriteLine("Slow In \t" + i.ToString() + "\t param3 \t" + slowInCh[i].Param3Value.ToString());
                }

                ////////////////////////

                /*
                //Fast Pulse Programmer Tab
                tw.WriteLine("pulsePeriodText" + "\t" + pulsePeriodText.Text);
                tw.WriteLine("out1SigName" + "\t" + out1SigName.Text);
                tw.WriteLine("out1OnTimeText" + "\t" + out1OnTimeText.Text);
                tw.WriteLine("out1DelayText" + "\t" + out1DelayText.Text);
                tw.WriteLine("out2SigName" + "\t" + out2SigName.Text);
                tw.WriteLine("out2OnTimeText" + "\t" + out2OnTimeText.Text);
                tw.WriteLine("out2DelayText" + "\t" + out2DelayText.Text);
                tw.WriteLine("out3SigName" + "\t" + out3SigName.Text);
                tw.WriteLine("out3OnTimeText" + "\t" + out3OnTimeText.Text);
                tw.WriteLine("out3DelayText" + "\t" + out3DelayText.Text);
                tw.WriteLine("out4SigName" + "\t" + out4SigName.Text);
                tw.WriteLine("out4OnTimeText" + "\t" + out4OnTimeText.Text);
                tw.WriteLine("out4DelayText" + "\t" + out4DelayText.Text);
                tw.WriteLine("in1SigName" + "\t" + in1SigName.Text);
                tw.WriteLine("in1OnTimeText" + "\t" + in1OnTimeText.Text);
                tw.WriteLine("in1DelayText" + "\t" + in1DelayText.Text);
                tw.WriteLine("in2SigName" + "\t" + in2SigName.Text);
                tw.WriteLine("in2OnTimeText" + "\t" + in2OnTimeText.Text);
                tw.WriteLine("in2DelayText" + "\t" + in2DelayText.Text);

                //Slow Pulse Programmer Tab
                tw.WriteLine("slow_pulsePeriodText" + "\t" + slow_pulsePeriodText.Text);
                tw.WriteLine("slow_out1SigName" + "\t" + slow_out1SigName.Text);
                tw.WriteLine("slow_out1OnTimeText" + "\t" + slow_out1OnTimeText.Text);
                tw.WriteLine("slow_out1DelayText" + "\t" + slow_out1DelayText.Text);
                tw.WriteLine("slow_out2SigName" + "\t" + slow_out2SigName.Text);
                tw.WriteLine("slow_out2OnTimeText" + "\t" + slow_out2OnTimeText.Text);
                tw.WriteLine("slow_out2DelayText" + "\t" + slow_out2DelayText.Text);
                tw.WriteLine("slow_out3SigName" + "\t" + slow_out3SigName.Text);
                tw.WriteLine("slow_out3OnTimeText" + "\t" + slow_out3OnTimeText.Text);
                tw.WriteLine("slow_out3DelayText" + "\t" + slow_out3DelayText.Text);
                tw.WriteLine("slow_out4SigName" + "\t" + slow_out4SigName.Text);
                tw.WriteLine("slow_out4OnTimeText" + "\t" + slow_out4OnTimeText.Text);
                tw.WriteLine("slow_out4DelayText" + "\t" + slow_out4DelayText.Text);
                tw.WriteLine("slow_in1SigName" + "\t" + slow_in1SigName.Text);
                tw.WriteLine("slow_in1OnTimeText" + "\t" + slow_in1OnTimeText.Text);
                tw.WriteLine("slow_in1DelayText" + "\t" + slow_in1DelayText.Text);
                tw.WriteLine("slow_in2SigName" + "\t" + slow_in2SigName.Text);
                tw.WriteLine("slow_in2OnTimeText" + "\t" + slow_in2OnTimeText.Text);
                tw.WriteLine("slow_in2DelayText" + "\t" + slow_in2DelayText.Text);
                */
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
                tw.WriteLine("DriveAmplitudeTextbox" + "\t" + DriveAmplitudeTextbox.Text);
                tw.WriteLine("DriveStateTextbox" + "\t" + DriveStateTextbox.Text);
                tw.WriteLine("S1PowerTextbox" + "\t" + S1PowerTextbox.Text);
                tw.WriteLine("S2PowerTextbox" + "\t" + S2PowerTextbox.Text);
                tw.WriteLine("S2QWPTextbox" + "\t" + S2QWPTextbox.Text);
                tw.WriteLine("PiPowerTextbox" + "\t" + PiPowerTextbox.Text);
                tw.WriteLine("Doppler35Textbox" + "\t" + Doppler35Textbox.Text);
                tw.WriteLine("CavityPowerTextbox" + "\t" + CavityPowerTextbox.Text);
                tw.WriteLine("BxTextbox" + "\t" + BxTextbox.Text);
                tw.WriteLine("ByTextbox" + "\t" + ByTextbox.Text);
                tw.WriteLine("BzTextbox" + "\t" + BzTextbox.Text);
                tw.WriteLine("LatticeDepthTextbox" + "\t" + LatticeDepthTextbox.Text);
                tw.WriteLine("LatticeQWPTextbox" + "\t" + LatticeQWPTextbox.Text);
                tw.WriteLine("ZtrapfrequencyTextbox" + "\t" + ZtrapfrequencyTextbox.Text);
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
                            //CONTROL TAB
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
                                //CurrentFeedforward370Offset.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "CurrentFeedforward370Gain":
                                //CurrentFeedforward370Gain.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "SideBeam370Power":
                                SideBeam370Power.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "CavityCoolingPowerControl":
                                CavityCoolingPowerControl.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "RecapturePowerSlider":
                                RecapturePowerSlider.Value = double.Parse(theString.Split('\t')[1]);
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
                            case "Sideband402Control":
                                Sideband402Control.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "BxSlider":
                                BxSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;
                            case "TickleSlider":
                                TickleSlider.Value = double.Parse(theString.Split('\t')[1]);
                                break;

                            //COUPLE DC TAB
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

                            //INDIVIDUAL DC TAB
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

                            //ELECTRODE SCAN TAB
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
                            
                            //SLIDER SCAN TAB
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
                            
                            //CORRELATOR TAB
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

                            //PULSE PROGRAMMER TAB
                            case "Sync period":
                                {
                                    LockInPertext1.Text = theString.Split('\t')[1];
                                    break;
                                }
                            case "Fast Overall Period":
                                {
                                    pulsePeriodText.Text = theString.Split('\t')[1];
                                    pulsePeriodVal = double.Parse(pulsePeriodText.Text);
                                    break;
                                }
                            case "Slow Overall Period":
                                {
                                    slow_pulsePeriodText.Text = theString.Split('\t')[1];
                                    slow_pulsePeriodVal = double.Parse(slow_pulsePeriodText.Text);
                                    syncTimeScales(pulsePeriodVal);
                                    break;
                                }
                            case "Fast Out":
                                {
                                      int i = int.Parse(theString.Split('\t')[1]);

                                      switch(theString.Split('\t')[2])
                                      {
                                          case "name":
                                              fastOutCh[i].Name = theString.Split('\t')[3];
                                              break;
                                          case "OptInd":
                                              fastOutCh[i].opt1Value = bool.Parse(theString.Split('\t')[3]);
                                              break;
                                          case "Opt2":
                                              fastOutCh[i].opt2Value = bool.Parse(theString.Split('\t')[3]);
                                              break;
                                          case "Opt3":
                                              fastOutCh[i].opt3Value = bool.Parse(theString.Split('\t')[3]);
                                              break;
                                          case "param1":
                                              fastOutCh[i].Param1Value = double.Parse(theString.Split('\t')[3]);
                                              break;
                                          case "param2":
                                              fastOutCh[i].Param2Value = double.Parse(theString.Split('\t')[3]);
                                              break;
                                          case "param3":
                                              fastOutCh[i].Param3Value = double.Parse(theString.Split('\t')[3]);
                                              break;
                                      }

                                      break;
                                }
                            case "Fast In":
                                {
                                    int i = int.Parse(theString.Split('\t')[1]);

                                    switch (theString.Split('\t')[2])
                                    {
                                        case "name":
                                            fastInCh[i].Name = theString.Split('\t')[3];
                                            break;
                                        case "OptInd":
                                            fastInCh[i].opt1Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "Opt2":
                                            fastInCh[i].opt2Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "Opt3":
                                            fastInCh[i].opt3Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param1":
                                            fastInCh[i].Param1Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param2":
                                            fastInCh[i].Param2Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param3":
                                            fastInCh[i].Param3Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                    }

                                    break;
                                }
                            case "Slow Out":
                                {
                                    int i = int.Parse(theString.Split('\t')[1]);

                                    switch (theString.Split('\t')[2])
                                    {
                                        case "name":
                                            slowOutCh[i].Name = theString.Split('\t')[3];
                                            break;
                                        case "OptInd":
                                            slowOutCh[i].opt1Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "Opt2":
                                            slowOutCh[i].opt2Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "Opt3":
                                            slowOutCh[i].opt3Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param1":
                                            slowOutCh[i].Param1Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param2":
                                            slowOutCh[i].Param2Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param3":
                                            slowOutCh[i].Param3Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                    }

                                    break;
                                }
                            case "Slow In":
                                {
                                    int i = int.Parse(theString.Split('\t')[1]);

                                    switch (theString.Split('\t')[2])
                                    {
                                        case "name":
                                            slowInCh[i].Name = theString.Split('\t')[3];
                                            break;
                                        case "OptInd":
                                            slowInCh[i].opt1Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "Opt2":
                                            slowInCh[i].opt2Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "Opt3":
                                            slowInCh[i].opt3Value = bool.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param1":
                                            slowInCh[i].Param1Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param2":
                                            slowInCh[i].Param2Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                        case "param3":
                                            slowInCh[i].Param3Value = double.Parse(theString.Split('\t')[3]);
                                            break;
                                    }

                                    break;
                                }


                                /*
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
                            case "out3SigName":
                                out3SigName.Text = theString.Split('\t')[1];
                                break;
                            case "out3OnTimeText":
                                out3OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "out3DelayText":
                                out3DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "out4SigName":
                                out4SigName.Text = theString.Split('\t')[1];
                                break;
                            case "out4OnTimeText":
                                out4OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "out4DelayText":
                                out4DelayText.Text = theString.Split('\t')[1];
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
                            case "slow_pulsePeriodText":
                                slow_pulsePeriodText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out1SigName":
                                slow_out1SigName.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out1OnTimeText":
                                slow_out1OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out1DelayText":
                                slow_out1DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out2SigName":
                                slow_out2SigName.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out2OnTimeText":
                                slow_out2OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out2DelayText":
                                slow_out2DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out3SigName":
                                slow_out3SigName.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out3OnTimeText":
                                slow_out3OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out3DelayText":
                                slow_out3DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out4SigName":
                                slow_out4SigName.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out4OnTimeText":
                                slow_out4OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_out4DelayText":
                                slow_out4DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_in1SigName":
                                slow_in1SigName.Text = theString.Split('\t')[1];
                                break;
                            case "slow_in1OnTimeText":
                                slow_in1OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_in1DelayText":
                                slow_in1DelayText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_in2SigName":
                                slow_in2SigName.Text = theString.Split('\t')[1];
                                break;
                            case "slow_in2OnTimeText":
                                slow_in2OnTimeText.Text = theString.Split('\t')[1];
                                break;
                            case "slow_in2DelayText":
                                slow_in2DelayText.Text = theString.Split('\t')[1];
                                break;
                                */
                            //CAMERA TAB
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

                            //DATA FILENAME TAB
                            case "DataFilenameChecklist":
                                for (int i = 1; i < theString.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries).GetLength(0); i++)
                                {
                                    DataFilenameChecklist.SetItemCheckState(int.Parse(theString.Split('\t')[i]),CheckState.Checked);
                                }
                                break;
                            case "DetuningTextbox":
                                DetuningTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "DriveAmplitudeTextbox":
                                DriveAmplitudeTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "DriveStateTextbox":
                                DriveStateTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "S1PowerTextbox":
                                S1PowerTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "S2PowerTextbox":
                                S2PowerTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "S2QWPTextbox":
                                S2QWPTextbox.Text = theString.Split('\t')[1];
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
                            case "LatticeQWPTextbox":
                                LatticeQWPTextbox.Text = theString.Split('\t')[1];
                                break;
                            case "ZtrapfrequencyTextbox":
                                ZtrapfrequencyTextbox.Text = theString.Split('\t')[1];
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

        //Method to Ramp a slider within a given time and number of poitns
        private void RampSlider(AdjustableSlider theSlider, int NumSteps, int time, double start, double end)
        {
            for (int i = 0; i < NumSteps; i++)
            {
                theSlider.Value = start + (end - start) * i / NumSteps;
                compensationAdjustedHelper();
                Thread.Sleep(time);
            }
            theSlider.Value = end;
        }

        //
        // Method to obtain filename from "Data Filename Control"
        //
        public string[] GetDataFilename(int what, string scantype)
        {
            string[] theString = new string[2];
            //path + root

            theString[0] = "f:\\raw_data\\Array\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\" + scantype;

            if (what == 1)
            {
                //theString[0] = DataFilenameFolderPath.Text + scantype;
                if (CommonFilenameSwitch.Value) { theString[1] = DataFilenameCommonRoot1.Text + " "; }
                else { theString[1] = DataFilenameCommonRoot2.Text + " "; }
            }
            else if (what == 2)
            {
                //theString[0] = DataFilenameFolderPathCorr.Text;
                theString[0] += DataFilenameCommonRoot1Corr.Text + " ";
            }

            //check if folder exists, if not create it
            if (!Directory.Exists(theString[0])) { Directory.CreateDirectory(theString[0]); }
            //set to current directory
            Directory.SetCurrentDirectory(theString[0]);

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
                        else { theString[1] += "corrdt=" + correlatorIntTimetext2.Text + " "; }
                        break;
                    case 3:
                        theString[1] += "ppT=" + pulsePeriodText.Text + " ";
                        break;
                    case 4:
                        theString[1] += "DrF=" + LockInFreqtext1.Text + " ";
                        break;
                    case 5:
                        theString[1] += "DrA=" + DriveAmplitudeTextbox.Text + " ";
                        break;
                    case 6:
                        theString[1] += DriveStateTextbox.Text + " ";
                        break;
                    case 7:
                        theString[1] += "det=" + DetuningTextbox.Text + " ";
                        break;
                    case 8:
                        theString[1] += "s1=" + S1PowerTextbox.Text + " ";
                        break;
                    case 9:
                        theString[1] += "s2=" + S2PowerTextbox.Text + " ";
                        break;
                    case 10:
                        theString[1] += "s2qwp=" + S2QWPTextbox.Text + " ";
                        break;
                    case 11:
                        theString[1] += "pi=" + PiPowerTextbox.Text + " ";
                        break;
                    case 12:
                        theString[1] += "dop35=" + Doppler35Textbox.Text + " ";
                        break;
                    case 13:
                        theString[1] += "cav=" + CavityPowerTextbox.Text + " ";
                        break;
                    case 14:
                        theString[1] += "Bx=" + BxTextbox.Text + " ";
                        break;
                    case 15:
                        theString[1] += "By=" + ByTextbox.Text + " ";
                        break;
                    case 16:
                        theString[1] += "Bz=" + BzTextbox.Text + " ";
                        break;
                    case 17:
                        theString[1] += "U=" + LatticeDepthTextbox.Text + " ";
                        break;
                    case 18:
                        theString[1] += "Uqwp=" + LatticeQWPTextbox.Text + " ";
                        break;
                    case 19:
                        theString[1] += "ar=" + ArrayTotalSlider.Value.ToString("F2") + " ";
                        break;
                    case 20:
                        theString[1] += "DY=" + TotalBiasSlider.Value.ToString("F2") + " ";
                        break;
                    case 21:
                        theString[1] += "DCQ=" + DCVertQuadSlider.Value.ToString("F2") + " ";
                        break;
                    case 22:
                        theString[1] += "DX=" + DXSlider.Value.ToString("F3") + " ";
                        break;
                    case 23:
                        theString[1] += "QTilt=" + QuadrupoleTilt.Value.ToString("F2") + " ";
                        break;
                    case 24:
                        theString[1] += "QRat=" + QuadTiltRatioSlider.Value.ToString("F2") + " ";
                        break;
                    case 25:
                        theString[1] += "ArRat=" + SnakeRatioSlider.Value.ToString("F2") + " ";
                        break;
                    case 26:
                        theString[1] += "ztrap=" + ZtrapfrequencyTextbox.Text + " ";
                        break;

                }
            }

            return theString;
        }

        //Method to save image from intensity graph as a text file
        private void SaveImageData(ThreadHelperClass theThreadHelper)
        {
            double[,] data = CameraForm.intensityPlot1.GetZData();

            //get filename from control parameters tab
            string[] filename = GetDataFilename(1, "ImageScan\\");

            try
            {
                //create text file
                //System.IO.StreamWriter tw = new System.IO.StreamWriter(filename[0] + theThreadHelper.threadName + " " + theThreadHelper.message + " SV=" + theThreadHelper.DoubleScanVariable[0,theThreadHelper.index].ToString("F3") + " " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");
                System.IO.StreamWriter tw = new System.IO.StreamWriter(theThreadHelper.threadName + " " + theThreadHelper.message + " SV=" + theThreadHelper.DoubleScanVariable[0, theThreadHelper.index].ToString("F3") + " " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                if (theThreadHelper != null)
                {
                    tw.WriteLine(theThreadHelper.DoubleScanVariable[0, theThreadHelper.index].ToString());
                }

                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1) - 1; j++)
                    {
                        tw.Write(data[i, j].ToString() + ",");
                    }
                    tw.WriteLine(data[i, data.GetLength(1) - 1]);
                }

                tw.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }


        //Method to save scan data
        private void SaveScanData(ThreadHelperClass threadHelper)
        {
            try
            {
                //get filename from control parameters tab
                string[] filename = GetDataFilename(1, threadHelper.folderPathExtra + threadHelper.threadName + "\\");
                //create text file
                System.IO.StreamWriter tw = new System.IO.StreamWriter(threadHelper.threadName + " Settings " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

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
                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " CorrSum " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

                    for (int i = 0; i < threadHelper.numPoints; i++)
                        tw.WriteLine(threadHelper.DoubleScanVariable[0, i] + "\t" + threadHelper.DoubleData[0, i]);

                    tw.Close();
                }

                if (threadHelper.message == "Camera" || threadHelper.message == "PMT & Camera")
                {
                    //Save fluorescence data
                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " FluorLog " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

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

                    tw = new System.IO.StreamWriter(filename[0] + threadHelper.threadName + " PosLog " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");

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

        private void LatticePositionSetSuggestedButton_Click(object sender, EventArgs e)
        {
            //reset to original values
            this.DCsliders[int.Parse(ElectrodeScanDC1TextBox.Text)].Value = double.Parse(LatticePositionNewValueText.Text);
            this.DCsliders[int.Parse(ElectrodeScanDC2TextBox.Text)].Value = double.Parse(LatticePositionNewValue2Text.Text);
            //update DAC
            compensationAdjustedHelper();
        }

        private void CorrelatorBinningPhaseSliderOut(object sender, EventArgs e)
        {
            theCorrelator.binningPhase = CorrelatorBinningPhaseSlider.Value;
            binningPhase = CorrelatorBinningPhaseSlider.Value;
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
        private void RecapturePowerSliderOutHelper()
        {
            Dev4AO4.OutputAnalogValue((double)RecapturePowerSlider.Value);
        }
        private void Sideband402ControlOut(object sender, EventArgs e)
        {
            VCOVVAoutputHelper((double)Sideband402Control.Value);
        }
        private void RecapturePowerSliderOut(object sender, EventArgs e)
        {
            RecapturePowerSliderOutHelper();
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
            SaveConfigurationFull("f:\\raw_data\\Array\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\", SaveFullConfigTextbox.Text);
        }

        private void ReadFullConfigButton_Click(object sender, EventArgs e)
        {
            //get file from dialog
            OpenFileDialog dialogB1 = new OpenFileDialog();
            dialogB1.InitialDirectory = "f:\\raw_data\\Array\\";

            if (dialogB1.ShowDialog() == DialogResult.OK)
            {
                ReadFullConfigTextbox.Text = dialogB1.FileName;
            }
            //update
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

        private void RecapturePowerSlider_Adjusted(object sender, EventArgs e)
        {
            Dev4AO4.OutputAnalogValue((double)RecapturePowerSlider.Value);
        }

        private void Repumper935Switch_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            Dev2DO7.OutputDigitalValue(!Repumper935Switch.Value);
        }

        private void ErrorOffset399SliderOut(object sender, EventArgs e)
        {
            Dev4AO2.OutputAnalogValue((double)ErrorOffset399Slider.Value);
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

        private void SavePulseSequencerSettings(string path)
        {
            try
            {
                System.IO.StreamWriter tw = new System.IO.StreamWriter(path + ".txt");

                tw.WriteLine(DateTime.Now);

                //In order of appearance, top left, to bottom right
                tw.WriteLine("Sync period" + "\t" + LockInPertext1.Text);
                tw.WriteLine("Fast Overall Period" + "\t" + pulsePeriodVal.ToString());
                tw.WriteLine("Slow Overall Period" + "\t" + slow_pulsePeriodVal.ToString());

                for (int i = 0; i < nfastchOut; i++)
                {
                    tw.WriteLine("Fast Out" + "\t" + i.ToString() + "\t" + "name" + "\t" + fastOutCh[i].Name);
                    tw.WriteLine("Fast Out" + "\t" + i.ToString() + "\t" + "OptInd" + "\t" + fastOutCh[i].opt1Value.ToString() );
                    tw.WriteLine("Fast Out" + "\t" + i.ToString() + "\t" + "Opt2" + "\t" + fastOutCh[i].opt2Value.ToString() );
                    tw.WriteLine("Fast Out" + "\t" + i.ToString() + "\t" + "Opt3" + "\t" + fastOutCh[i].opt3Value.ToString());
                    tw.WriteLine("Fast Out" + "\t" + i.ToString() + "\t" + "param1" + "\t" + fastOutCh[i].Param1Value.ToString());
                    tw.WriteLine("Fast Out" + "\t" + i.ToString() + "\t" + "param2" + "\t" + fastOutCh[i].Param2Value.ToString());
                    tw.WriteLine("Fast Out" + "\t" + i.ToString() + "\t" + "param3" + "\t" + fastOutCh[i].Param3Value.ToString());

                }
                for (int i = 0; i < nfastchIn; i++)
                {
                    tw.WriteLine("Fast In" + "\t" + i.ToString() + "\t" + "name" + "\t" + fastInCh[i].Name);
                    tw.WriteLine("Fast In" + "\t" + i.ToString() + "\t" + "OptInd" + "\t" + fastInCh[i].opt1Value.ToString());
                    tw.WriteLine("Fast In" + "\t" + i.ToString() + "\t" + "Opt2" + "\t" + fastInCh[i].opt2Value.ToString());
                    tw.WriteLine("Fast In" + "\t" + i.ToString() + "\t" + "Opt3" + "\t" + fastInCh[i].opt3Value.ToString());
                    tw.WriteLine("Fast In" + "\t" + i.ToString() + "\t" + "param1" + "\t" + fastInCh[i].Param1Value.ToString());
                    tw.WriteLine("Fast In" + "\t" + i.ToString() + "\t" + "param2" + "\t" + fastInCh[i].Param2Value.ToString());
                    tw.WriteLine("Fast In" + "\t" + i.ToString() + "\t" + "param3" + "\t" + fastInCh[i].Param3Value.ToString());
                }
                for (int i = 0; i < nslowchOut; i++)
                {
                    tw.WriteLine("Slow Out" + "\t" + i.ToString() + "\t" + "name" + "\t" + slowOutCh[i].Name);
                    tw.WriteLine("Slow Out" + "\t" + i.ToString() + "\t" + "OptInd" + "\t" + slowOutCh[i].opt1Value.ToString());
                    tw.WriteLine("Slow Out" + "\t" + i.ToString() + "\t" + "Opt2" + "\t" + slowOutCh[i].opt2Value.ToString());
                    tw.WriteLine("Slow Out" + "\t" + i.ToString() + "\t" + "Opt3" + "\t" + slowOutCh[i].opt3Value.ToString());
                    tw.WriteLine("Slow Out" + "\t" + i.ToString() + "\t" + "param1" + "\t" + slowOutCh[i].Param1Value.ToString());
                    tw.WriteLine("Slow Out" + "\t" + i.ToString() + "\t" + "param2" + "\t" + slowOutCh[i].Param2Value.ToString());
                    tw.WriteLine("Slow Out" + "\t" + i.ToString() + "\t" + "param3" + "\t" + slowOutCh[i].Param3Value.ToString());
                }
                for (int i = 0; i < nslowchIn; i++)
                {
                    tw.WriteLine("Slow In" + "\t" + i.ToString() + "\t" + "name" + "\t" + slowInCh[i].Name);
                    tw.WriteLine("Slow In" + "\t" + i.ToString() + "\t" + "OptInd" + "\t" + slowInCh[i].opt1Value.ToString());
                    tw.WriteLine("Slow In" + "\t" + i.ToString() + "\t" + "Opt2" + "\t" + slowInCh[i].opt2Value.ToString());
                    tw.WriteLine("Slow In" + "\t" + i.ToString() + "\t" + "Opt3" + "\t" + slowInCh[i].opt3Value.ToString());
                    tw.WriteLine("Slow In" + "\t" + i.ToString() + "\t" + "param1" + "\t" + slowInCh[i].Param1Value.ToString());
                    tw.WriteLine("Slow In" + "\t" + i.ToString() + "\t" + "param2" + "\t" + slowInCh[i].Param2Value.ToString());
                    tw.WriteLine("Slow In" + "\t" + i.ToString() + "\t" + "param3" + "\t" + slowInCh[i].Param3Value.ToString());
                }

                tw.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }



        //
        // Shut down camera when closing the application
        //

        private void Form1_Closing(object sender, EventArgs e)
        {
            //Save slider values and textbox values for the application
            SaveConfigurationFull("f:\\raw_data\\Array\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\", SaveFullConfigTextbox.Text);
            //Shutdown camera
            if (Camera != null)
            {
                Camera.AppShutDown();
            }
        }

        private void Form1_Disposed(object sender, System.EventArgs e)
        {
            //Save slider values and textbox values for the application
            SaveConfigurationFull("f:\\raw_data\\Array\\" + DateTime.Now.ToString("yyyy") + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\", SaveFullConfigTextbox.Text);
        }

        ///////////////////////////
        //
        //MULTITHREADING CODE BELOW
        //
        ///////////////////////////

        //
        // FORM CALLBACK FUNCTIONS
        //

        private void UpdateLabelCallbackFn(string theString, System.Windows.Forms.Label theLabel)
        {
            theLabel.Text = theString;
        }

        private void GetComboBoxTextCallbackFn(ThreadHelperClass theThreadHelper, ComboBox theComboBox)
        {
            //Get text from combo box
            theThreadHelper.message5 = theComboBox.Text;
        }

        private void ScanUpdateCallbackFn(ThreadHelperClass theThreadHelper)
        {
            //update slider
            theThreadHelper.theSlider.Value = theThreadHelper.DoubleScanVariable[0, theThreadHelper.index];
            if (theThreadHelper.numScanVar > 1)
            {
                theThreadHelper.theSlider2.Value = theThreadHelper.DoubleScanVariable[1, theThreadHelper.index];
            }
            //Button Indicator
            if (!(theThreadHelper.theButton == null))
            {
                int updateInt = theThreadHelper.index + 1;
                theThreadHelper.theButton.Text = "Scanning..." + updateInt.ToString();
            }
            //update DAC
            UpdateAll();
        }

        private void ScanResetCallbackFn(ThreadHelperClass theThreadHelper)
        {
            if (!(theThreadHelper.theButton == null))
            {
                theThreadHelper.theButton.BackColor = System.Drawing.Color.WhiteSmoke;
                theThreadHelper.theButton.Text = "Start Scan";
            }
            //reset to original values
            theThreadHelper.theSlider.Value = theThreadHelper.KeepDoubles[0];
            if (theThreadHelper.numScanVar > 1)
            {
                theThreadHelper.theSlider2.Value = theThreadHelper.KeepDoubles[1];
            }
            //call to update
            UpdateAll();
        }


        private void PMTPlotCallbackFn(ThreadHelperClass theThreadHelper)
        {
            //display count
            CameraForm.PMTcountBox.Text = theThreadHelper.SingleDouble.ToString();
            //update PMT plot
            CameraForm.PMTcountGraph.PlotYAppend(theThreadHelper.SingleDouble);
        }

        private void ExpSeqIntensityGraphUpdateCallbackFn(ThreadHelperClass theThreadHelper)
        {
            //pmt1
            CameraForm.ExpSeqIntensityPlot1.Plot(theThreadHelper.DoubleDataArray[0], theThreadHelper.min[0], (theThreadHelper.max[0] - theThreadHelper.min[0]) / (theThreadHelper.numPoints - 1), 0, 1);
            //pmt2
            CameraForm.ExpSeqIntensityPlot2.Plot(theThreadHelper.DoubleDataArray[1], theThreadHelper.min[0], (theThreadHelper.max[0] - theThreadHelper.min[0]) / (theThreadHelper.numPoints - 1), 0, 1);
            //sum
            CameraForm.ExpSeqIntensityPlot3.Plot(theThreadHelper.DoubleDataArray[2], theThreadHelper.min[0], (theThreadHelper.max[0] - theThreadHelper.min[0]) / (theThreadHelper.numPoints - 1), 0, 1);
            //difference
            CameraForm.ExpSeqIntensityPlot4.Plot(theThreadHelper.DoubleDataArray[3], theThreadHelper.min[0], (theThreadHelper.max[0] - theThreadHelper.min[0]) / (theThreadHelper.numPoints - 1), 0, 1);
        }

        public void ExpSeqViewScatterGraphUpdateCallbackFn(ThreadHelperClass theThreadHelper)
        {
            //which PMT configuration to view
            int PMTConfig;
            switch (CameraForm.ExpSeqViewPMTConfig.Text)
            {
                case "PMT 1":
                    PMTConfig = 0;
                    break;
                case "PMT 2":
                    PMTConfig = 1;
                    break;
                case "SUM":
                    PMTConfig = 2;
                    break;
                case "DIFFERENCE":
                    PMTConfig = 3;
                    break;
                default:
                    PMTConfig = 0;
                    break;
            }

            //which scan index
            int ScanIndex = int.Parse(CameraForm.ExpSeqViewScanIndex.Text);
            if (ScanIndex < 1) ScanIndex = 1;
            if (ScanIndex > theThreadHelper.numPoints) ScanIndex = theThreadHelper.numPoints ;

            int lengthofdata = theCorrelator.phcountarrayCh1.Length;
            double[] dataPlot = new double[lengthofdata];
            double[] dataPlotErr = new double[lengthofdata];
            for (int i = 0; i < lengthofdata; i++)
            {
                dataPlot[i] = theThreadHelper.DoubleDataArray[PMTConfig][ScanIndex-1, i];
                dataPlotErr[i] = theThreadHelper.DoubleDataArray[PMTConfig+4][ScanIndex - 1, i];
            }

            //scan variable display
            CameraForm.ExpSeqViewScanVariable.Text = theThreadHelper.DoubleScanVariable[0, ScanIndex - 1].ToString();

            //plot
            CameraForm.ExpSeqWaveFormGraph.PlotY(dataPlot);
        }


        private void ScanResultsGraphCallbackFn(ThreadHelperClass theThreadHelper)
        {
            lock (theThreadHelper)
            {
                try
                {
                    CameraForm.ScanResultsGraph.Plots[0].Visible = true;
                    //plot
                    CameraForm.ScanResultsGraph.PlotXYAppend(theThreadHelper.DoubleScanVariable[0, theThreadHelper.index], theThreadHelper.DoubleData[0, theThreadHelper.index]);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                Monitor.PulseAll(theThreadHelper);
            }
        }

        // GENERAL SCAN FUNCTIONS

        private void initializeDataStreams(ThreadHelperClass theThreadHelper)
        {
            //if running camera, initialize, and clear fluor and position graphs
            if (theThreadHelper.message == "Camera" || theThreadHelper.message == "CameraImage")
            {
                // clear graphs
                CameraForm.FluorescenceGraph.ClearData();
                CameraForm.PositionGraph.ClearData();
                if (Camera.AppInitialize())
                {
                    CameraInitializeHelper();
                }
            }

            if (theThreadHelper.message == "Correlator:Sum" || theThreadHelper.message == "Correlator:Channels")
            {
                //Initialize parameters to values entered under "Correlator" Tab  
                //if correlator returns false for init, abort scan
                if (!CorrelatorParameterInit())
                {
                    //end scan
                    theThreadHelper.ShouldBeRunningFlag = false;
                    //show message
                    MessageBox.Show("Correlator Init returned false");
                }
            }
        }

        private void getDatafromStream(ThreadHelperClass theThreadHelper)
        {
            if (theThreadHelper.message == "PMT")
            {
                for (int i = 0; i < theThreadHelper.numAverage; i++)
                {
                    //get reading from GPIB counter
                    gpibdevice.simpleRead(21);
                    //get decimal number
                    theThreadHelper.SingleDouble = gpibDoubleResult();
                    //load result in array
                    theThreadHelper.DoubleData[0, theThreadHelper.index] += theThreadHelper.SingleDouble;

                    try
                    {
                        this.BeginInvoke(new MyDelegateThreadHelper(PMTPlotCallbackFn), theThreadHelper);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    //update Sigma array
                    theThreadHelper.DoubleData[1, theThreadHelper.index] += Math.Pow(theThreadHelper.SingleDouble, 2);
                }
                //finalize single point average and standard deviation
                theThreadHelper.DoubleData[0, theThreadHelper.index] = theThreadHelper.DoubleData[0, theThreadHelper.index] / theThreadHelper.numAverage;
                theThreadHelper.DoubleData[1, theThreadHelper.index] = Math.Sqrt(theThreadHelper.DoubleData[1, theThreadHelper.index] / theThreadHelper.numAverage - Math.Pow(theThreadHelper.DoubleData[0, theThreadHelper.index], 2));

                lock (theThreadHelper)
                {
                    //display count, plot
                    try
                    {
                        this.BeginInvoke(new MyDelegateThreadHelper(ScanResultsGraphCallbackFn), theThreadHelper);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    Monitor.Wait(theThreadHelper);
                }
            }
            // if Camera selected run Camera acquisition
            if (theThreadHelper.message == "Camera" || theThreadHelper.message == "CameraImage")
            {
                CameraAcquisitionHelper();
                if (theThreadHelper.message == "CameraImage")
                {
                    SaveImageData(theThreadHelper);
                }
            }

            // if AI selected, get reading from NI card
            if (theThreadHelper.message == "Dev3AI2")
            {
                theThreadHelper.DoubleData[0, theThreadHelper.index] = Dev3AI2.ReadAnalogValue();
            }

            // if Correlator:Sum selected, get reading from correlator, and sum bins
            if (theThreadHelper.message == "Correlator:Sum")
            {
                //Get results into correlator object
                CorrelatorGetResultsHelper();

                //Correlator Plots
                try
                {
                    this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallbackCh1));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                //Put sum of two channels data in Thread array
                theThreadHelper.DoubleData[0, theThreadHelper.index] = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;

                //plot
                lock (theThreadHelper)
                {
                    //display count, plot
                    try
                    {
                        this.BeginInvoke(new MyDelegateThreadHelper(ScanResultsGraphCallbackFn),theThreadHelper);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    Monitor.Wait(theThreadHelper);
                }
            }

            // if Correlator:Channels selected, get reading from correlator, and sum bins
            if (theThreadHelper.message == "Correlator:Channels")
            {
                //Get results into correlator object
                CorrelatorGetResultsHelper();

                //Correlator Plots
                try
                {
                    this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallbackCh1));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                //Put sum of two channels data in Thread array
                theThreadHelper.DoubleData[0, theThreadHelper.index] = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;

                //plot
                lock (theThreadHelper)
                {
                    //display count, plot
                    try
                    {
                        this.BeginInvoke(new MyDelegateThreadHelper(ScanResultsGraphCallbackFn), theThreadHelper);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    Monitor.Wait(theThreadHelper);
                }
                //save
                SaveCorrelatorData(theThreadHelper.folderPathExtra + "correlator\\" + theThreadHelper.threadName + "=" + theThreadHelper.DoubleScanVariable[0, theThreadHelper.index].ToString("F3") + "\\");
            }

        }

        private void StopDataStreams()
        {
            // if camera is running stop it
            if (CameraThreadHelper.ShouldBeRunningFlag)
            {
                StopCameraThread();
                try
                {
                    CameraThreadHelper.theThread.Abort();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }

            //if correlator is running stop it
            if (CorrelatorThreadHelper.ShouldBeRunningFlag)
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

        private AdjustableSlider getSliderfromText(string SliderName)
        {
            AdjustableSlider theSlider;

            switch (SliderName)
            {
                case "DXSlider":
                    theSlider = this.DXSlider;
                    break;
                case "ArrayTotalSlider":
                    theSlider = this.ArrayTotalSlider;
                    break;
                case "DCVertDipoleSlider":
                    theSlider = this.DCVertDipoleSlider;
                    break;
                case "DCVertQuadSlider":
                    theSlider = this.DCVertQuadSlider;
                    break;
                case "TotalBiasSlider":
                    theSlider = this.TotalBiasSlider;
                    break;
                case "TrapHeightSlider":
                    theSlider = this.TrapHeightSlider;
                    break;
                case "QuadrupoleTilt":
                    theSlider = this.QuadrupoleTilt;
                    break;
                case "QuadTiltRatioSlider":
                    theSlider = this.QuadTiltRatioSlider;
                    break;
                case "SnakeRatioSlider":
                    theSlider = this.SnakeRatioSlider;
                    break;
                case "RightFingersSlider":
                    theSlider = this.RightFingersSlider;
                    break;
                case "LeftFingersSlider":
                    theSlider = this.LeftFingersSlider;
                    break;
                case "SnakeOnlySlider":
                    theSlider = this.SnakeOnlySlider;
                    break;
                case "TransferCavity":
                    theSlider = this.TransferCavity;
                    break;
                case "RepumperSlider":
                    theSlider = this.RepumperSlider;
                    break;
                case "RecapturePowerSlider":
                    theSlider = this.RecapturePowerSlider;
                    break;
                case "SideBeam370Power":
                    theSlider = this.SideBeam370Power;
                    break;
                case "LatticePowerControl":
                    theSlider = this.LatticePowerControl;
                    break;
                case "CavityCoolingPowerControl":
                    theSlider = this.CavityCoolingPowerControl;
                    break;
                case "Sideband402Control":
                    theSlider = this.Sideband402Control;
                    break;
                case "RamanSlider":
                    theSlider = this.RamanSlider;
                    break;
                case "BxSlider":
                    theSlider = this.BxSlider;
                    break;
                case "TickleSlider":
                    theSlider = this.TickleSlider;
                    break;
                case "CorrelatorBinningPhaseSlider":
                    theSlider = this.CorrelatorBinningPhaseSlider;
                    break;
                case "DC0":
                    theSlider = this.DCsliders[0];
                    break;
                case "DC1":
                    theSlider = this.DCsliders[1];
                    break;
                case "DC2":
                    theSlider = this.DCsliders[2];
                    break;
                case "DC3":
                    theSlider = this.DCsliders[3];
                    break;
                case "DC4":
                    theSlider = this.DCsliders[4];
                    break;
                case "DC5":
                    theSlider = this.DCsliders[5];
                    break;
                case "DC6":
                    theSlider = this.DCsliders[6];
                    break;
                case "DC7":
                    theSlider = this.DCsliders[7];
                    break;
                case "DC8":
                    theSlider = this.DCsliders[8];
                    break;
                case "DC9":
                    theSlider = this.DCsliders[9];
                    break;
                case "DC10":
                    theSlider = this.DCsliders[10];
                    break;
                case "DC11":
                    theSlider = this.DCsliders[11];
                    break;
                default:
                    theSlider = new AdjustableSlider();
                    break;
            }

            return theSlider;
        }

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

        private void SaveCorrelatorData(string foldernameaddon)
        {
            //get filename from control parameters tab
            string[] filename = GetDataFilename(2, foldernameaddon);
            //create text file
            System.IO.StreamWriter tw = new System.IO.StreamWriter("Correlator Data " + filename[1] + DateTime.Now.ToString("HHmmss") + " " + ".txt");
            for (int j = 0; j < ncorrbins; j++)
                tw.WriteLine(theCorrelator.phcountarrayCh1[j] + "\t" + theCorrelator.phcountarrayCh2[j]);
            tw.Write("\n");
            tw.Close();
        }

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




            /*
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
             * */
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
            if (nbinSwitch.Value)
            { ncorrbins = int.Parse(ncorrbinsText.Text); }
            else
            { ncorrbins = int.Parse(ncorrbinsText2.Text); }
            theCorrelator.lshiftreg = ncorrbins;

            // initialize the Ch1 and Ch2 log variables:
            historyCounter = 0;
            corrampCh1history = new double[ncorrbins][];
            corrampCh2history = new double[ncorrbins][];
            for (int i = 0; i < ncorrbins; i++)
            {
                corrampCh1history[i] = new double[1000];
                corrampCh2history[i] = new double[1000];
            }

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

            theCorrelator.PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * pulsePeriodVal, 0));
            theCorrelator.slow_PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * slow_pulsePeriodVal, 0));

            for(int i=0; i < nfastchOut; i++)
            {
               theCorrelator.onTimeOut[i] = (uint)(Math.Round(theCorrelator.ok.P * fastOutCh[i].Param2Value, 0));
               theCorrelator.delayOut[i] = (uint)(Math.Round(theCorrelator.ok.P * fastOutCh[i].Param3Value, 0));
               theCorrelator.subperiodOut[i] = (uint)(Math.Round(theCorrelator.ok.P * fastOutCh[i].Param1Value, 0));
            }
            for (int i = 0; i < nfastchIn; i++)
            {
                theCorrelator.onTimeIn[i] = (uint)(Math.Round(theCorrelator.ok.P * fastInCh[i].Param2Value, 0));
                theCorrelator.delayIn[i] = (uint)(Math.Round(theCorrelator.ok.P * fastInCh[i].Param3Value, 0));
                theCorrelator.subperiodIn[i] = (uint)(Math.Round(theCorrelator.ok.P * fastInCh[i].Param1Value, 0));
            }
            for (int i = 0; i < nslowchOut; i++)
            {
                theCorrelator.slow_onTimeOut[i] = (uint)(slowOutCh[i].Param2Value);
                theCorrelator.slow_delayOut[i] = (uint)(slowOutCh[i].Param3Value);
                theCorrelator.slow_subperiodOut[i] = (uint)(slowOutCh[i].Param1Value);
            }
            for (int i = 0; i < nslowchIn; i++)
            {
                theCorrelator.slow_onTimeIn[i] = (uint)(slowInCh[i].Param2Value);
                theCorrelator.slow_delayIn[i] = (uint)(slowInCh[i].Param3Value);
                theCorrelator.slow_subperiodIn[i] = (uint)(slowInCh[i].Param1Value); ;
            }

            /*
             
            theCorrelator.PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(pulsePeriodText.Text), 0));

            theCorrelator.onTimeOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1OnTimeText.Text), 0));
            theCorrelator.onTimeOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2OnTimeText.Text), 0));
            theCorrelator.onTimeOut[2] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out3OnTimeText.Text), 0));
            theCorrelator.onTimeOut[3] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out4OnTimeText.Text), 0));

            theCorrelator.delayOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1DelayText.Text), 0));
            theCorrelator.delayOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2DelayText.Text), 0));
            theCorrelator.delayOut[2] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out3DelayText.Text), 0));
            theCorrelator.delayOut[3] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out4DelayText.Text), 0));

            theCorrelator.onTimeIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1OnTimeText.Text), 0));
            theCorrelator.onTimeIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2OnTimeText.Text), 0));
            theCorrelator.delayIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1DelayText.Text), 0));
            theCorrelator.delayIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2DelayText.Text), 0));


            theCorrelator.slow_PulseClkDiv = uint.Parse(slow_pulsePeriodText.Text);

            theCorrelator.slow_onTimeOut[0] = uint.Parse(slow_out1OnTimeText.Text);
            theCorrelator.slow_onTimeOut[1] = uint.Parse(slow_out2OnTimeText.Text);
            theCorrelator.slow_onTimeOut[2] = uint.Parse(slow_out3OnTimeText.Text);
            theCorrelator.slow_onTimeOut[3] = uint.Parse(slow_out4OnTimeText.Text);

            theCorrelator.slow_delayOut[0] = uint.Parse(slow_out1DelayText.Text);
            theCorrelator.slow_delayOut[1] = uint.Parse(slow_out2DelayText.Text);
            theCorrelator.slow_delayOut[2] = uint.Parse(slow_out3DelayText.Text);
            theCorrelator.slow_delayOut[3] = uint.Parse(slow_out4DelayText.Text);

            theCorrelator.slow_onTimeIn[0] = uint.Parse(slow_in1OnTimeText.Text);
            theCorrelator.slow_onTimeIn[1] = uint.Parse(slow_in2OnTimeText.Text);
            theCorrelator.slow_delayIn[0] = uint.Parse(slow_in1DelayText.Text);
            theCorrelator.slow_delayIn[1] = uint.Parse(slow_in2DelayText.Text);

            */

            theCorrelator.bound1 = int.Parse(correlatorBound1text.Text);
            theCorrelator.bound2 = int.Parse(correlatorBound2text.Text);



            //Set boolean in correlator for sync signal source
            if (syncSrcSw.Value) { theCorrelator.syncSrcChoose = true; }
            else { theCorrelator.syncSrcChoose = false; }

            //Attempt Initialize
            bool auxInitBool = true;
            if (nbinSwitch.Value)
            {
                if (chooseCode.Value)
                { auxInitBool = theCorrelator.Init(correlatorBitFilePath.Text); }
                else
                { auxInitBool = theCorrelator.Init(correlatorBitFilePathB.Text); }
            }
            else
            {
                if (chooseCode.Value)
                { auxInitBool = theCorrelator.Init(correlatorBitFilePath_manybins.Text); }
                else
                { auxInitBool = theCorrelator.Init(correlatorBitFilePath_manybinsB.Text); }
            }

            //return status of init
            return auxInitBool;
        }

        private void CorrelatorExecute()
        {
            //clear graph
            CameraForm.ScanResultsGraph.ClearData();
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



            // initialize x-axis array (bin indices) for plotting correlator curves as XY plots
            double[] corrbins = new double[ncorrbins];
            for (int cbin = 0; cbin < ncorrbins; cbin++)
            {
                corrbins[cbin] = cbin + 1;
            }

            // retrieve the averaged data on the plot so far:
            double[] prevCorrDataCh1 = CameraForm.scatterGraphNormCorrSig.Plots[0].GetYData();
            double[] prevCorrDataCh2 = CameraForm.scatterGraphNormCorrSig.Plots[1].GetYData();
            double[] prevCorrDataDiff = CameraForm.scatterGraphNormCorrSig.Plots[2].GetYData();
            double[] prevCorrDataSum = CameraForm.scatterGraphNormCorrSig.Plots[3].GetYData();
            // new correlator trace that came in from the FPGA:
            double[] newCorrDataCh1 = theCorrelator.phcountarrayCh1;
            double[] newCorrDataCh2 = theCorrelator.phcountarrayCh2;
            double[] newCorrDataDiff = new double[ncorrbins];
            double[] newCorrDataSum = new double[ncorrbins];
            double[] newnormSig = new double[ncorrbins];
            // initialize averaged data vectors and error vectors
            double[] avgCorrDataCh1 = new double[ncorrbins];
            double[] sqferrCorrDataCh1 = new double[ncorrbins];
            double[] errCorrDataCh1 = new double[ncorrbins];
            double[] avgCorrDataCh2 = new double[ncorrbins];
            double[] sqferrCorrDataCh2 = new double[ncorrbins];
            double[] errCorrDataCh2 = new double[ncorrbins];
            double[] avgCorrDataDiff = new double[ncorrbins];
            double[] errCorrDataDiff = new double[ncorrbins];
            double[] avgCorrDataSum = new double[ncorrbins];
            double[] errCorrDataSum = new double[ncorrbins];
            double[] avgnormSig = new double[ncorrbins];
            double[] errnormSig = new double[ncorrbins];
            // for plotting:
            double[] corrDataCh1ForPlot = new double[ncorrbins * 4];
            double[] corrDataCh2ForPlot = new double[ncorrbins * 4];
            double[] corrDataDiffForPlot = new double[ncorrbins * 4];
            double[] corrDataSumForPlot = new double[ncorrbins * 4];
            double[] normSigForPlot = new double[ncorrbins*4];
            double[] corrbinsForPlot = new double[ncorrbins * 4];
            // initialize bin variables for sin amplitude estimation:
            double binA = 0;
            double binB = 0;
            double errbinA = 0;
            double errbinB = 0;
            // initialize extracted normalized signal amplitude:
            double normAmplitude = 0;
            double normAmplitudeErr = 0;
            // initialize total counts
            double ctot = 0;

            // read in binning phase:
            binningPhase = CorrelatorBinningPhaseSlider.Value;

            //update history counter
            historyCounter = historyCounter + 1;
            // expand Ch1 and Ch2 log variables if needed
            if(historyCounter >= corrampCh1history[0].Length)
            {
                for (int i = 0; i < ncorrbins; i++)
                {
                    int oldlength = corrampCh1history[i].Length;

                    double[] temp1 = corrampCh1history[i];
                    corrampCh1history[i] = new double[oldlength+1000];
                    temp1.CopyTo(corrampCh1history[i],0);

                    double[] temp2 = corrampCh2history[i];
                    corrampCh2history[i] = new double[oldlength + 1000];
                    temp2.CopyTo(corrampCh1history[i], 0);
                }
            }

            // compute functions of data and statistics bin by bin:
            for (int j = 0; j < ncorrbins; j++)
            {
                // put new Ch1 and Ch2 data into the log vectors
                corrampCh1history[j][historyCounter] = newCorrDataCh1[j];
                corrampCh2history[j][historyCounter] = newCorrDataCh2[j];

                // calculate the difference and sum counts for new data
                newCorrDataDiff[j] = newCorrDataCh1[j] - newCorrDataCh2[j];
                newCorrDataSum[j] = newCorrDataCh1[j] + newCorrDataCh2[j];
                // normalized balanced signal for new data:  (s1 - s2) / (s1 + s2)
                newnormSig[j] = newCorrDataDiff[j] / (newCorrDataSum[j] - 2 * (int.Parse(textBoxPMT1back.Text)));
                
                /////////// Statistics of Ch1 and Ch2: /////////////////////////
                // compute statistical average of all Ch1 and Ch2 data collected since last reset:
                avgCorrDataCh1[j] = sampleAve(corrampCh1history[j], historyCounter);
                avgCorrDataCh2[j] = sampleAve(corrampCh2history[j], historyCounter);
                // compute fractional square error of Ch1 and Ch2 as standard error of the mean over data collected since last reset:
                sqferrCorrDataCh1[j] = Math.Pow(sampleStd(corrampCh1history[j], historyCounter) / avgCorrDataCh1[j], 2) / historyCounter;
                sqferrCorrDataCh2[j] = Math.Pow(sampleStd(corrampCh2history[j], historyCounter) / avgCorrDataCh2[j], 2) / historyCounter;
                // actual error of Ch1 and Ch2:
                errCorrDataCh1[j] = avgCorrDataCh1[j] * Math.Sqrt(sqferrCorrDataCh1[j]);
                errCorrDataCh2[j] = avgCorrDataCh2[j] * Math.Sqrt(sqferrCorrDataCh2[j]);

                ///////// Statistics of sum and difference: //////////
                avgCorrDataDiff[j] = avgCorrDataCh1[j] - avgCorrDataCh2[j];
                avgCorrDataSum[j] = avgCorrDataCh1[j] + avgCorrDataCh2[j];
                errCorrDataDiff[j] = Math.Sqrt(Math.Pow(errCorrDataCh1[j],2) + Math.Pow(errCorrDataCh2[j],2));
                errCorrDataSum[j] = errCorrDataDiff[j];
              
                ///////// Statistics of normalized balanced signal: //////////
                avgnormSig[j] = (avgCorrDataDiff[j] - (int.Parse(textBoxPMT1back.Text) - int.Parse(textBoxPMT2back.Text))) / (avgCorrDataSum[j] - (int.Parse(textBoxPMT1back.Text) + int.Parse(textBoxPMT2back.Text)));
                errnormSig[j] = (2*avgCorrDataCh1[j]*avgCorrDataCh2[j])/((avgCorrDataCh1[j]+avgCorrDataCh2[j])*(avgCorrDataCh1[j]+avgCorrDataCh2[j])) * Math.Sqrt( sqferrCorrDataCh1[j] + sqferrCorrDataCh2[j] );

            }

            for (int i = 0; i <= (ncorrbins - 1); i++)
            {
                if ((i + 1 < ncorrbins / 2 + 1 + binningPhase) && (i + 1 > binningPhase))
                {
                    binA += avgnormSig[i];
                    errbinA += Math.Pow(errnormSig[i],2);
                }
                else
                {   
                    binB += avgnormSig[i];
                    errbinB += Math.Pow(errnormSig[i],2);
                }
            }
            normAmplitude = (binA-binB)/ncorrbins*3.1415/2;
            normAmplitudeErr = Math.Sqrt(errbinA + errbinB) / ncorrbins * 3.1415 / 2;

            // plot the new correlator data 
            CameraForm.CorrelatorGraph.Plots[0].PlotY(newCorrDataCh1);
            CameraForm.CorrelatorGraph.Plots[1].PlotY(newCorrDataCh2);
            CameraForm.CorrelatorGraph.Plots[2].PlotY(newCorrDataDiff);
            CameraForm.CorrelatorGraph.Plots[3].PlotY(newCorrDataSum);

            // Display count RATE as a function of time
            CameraForm.PMTcountGraph.Plots[0].PlotYAppend(theCorrelator.totalCountsCh1 / theCorrelator.IntTime * 1000);
            CameraForm.PMTcountGraph.Plots[1].PlotYAppend(theCorrelator.totalCountsCh2 / theCorrelator.IntTime * 1000);
            ctot = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;
            CameraForm.PMTcountGraph.Plots[2].PlotYAppend((theCorrelator.totalCountsCh1 - theCorrelator.totalCountsCh2) / theCorrelator.IntTime * 1000);
            CameraForm.PMTcountGraph.Plots[3].PlotYAppend(ctot / theCorrelator.IntTime * 1000);
            
            //Display total counts in correlator tab
            CameraForm.correlatorTotalCounts.Text = ctot.ToString();
            //Display counts/s above Graph
            double ctotn = ctot / theCorrelator.IntTime * 1000;
            CameraForm.PMTcountBox.Text = ctotn.ToString();
            //Display correlator amplitude
            //CameraForm.correlatorDecompMerit.Text = theCorrelator.decompMerit.ToString() + "+/-" + theCorrelator.decompMeritErr.ToString() + " %";
            double roundedAmp = Math.Round(normAmplitude, 3);
            double roundedAmpErr = Math.Round(normAmplitudeErr, 3);
            CameraForm.correlatorDecompMerit.Text = roundedAmp.ToString() + "+/-" + roundedAmpErr.ToString();

            // display recapture lock feedback counts:
            recaplockcounts_display.Text = theCorrelator.recaplockcnts.ToString();

            // plot the averaged correlator data
            // if this is the first trace, just plot it, otherwise add it to the averaged data from before:
            for(int i = 0; i<ncorrbins*4; i=i+4)
            {
                corrbinsForPlot[i] = corrbins[i / 4];
                corrbinsForPlot[i+1] = corrbins[i / 4];
                corrbinsForPlot[i+2] = corrbins[i / 4];
                corrbinsForPlot[i+3] = corrbins[i / 4];
                normSigForPlot[i] = avgnormSig[i / 4];
                normSigForPlot[i + 1] = avgnormSig[i / 4] + errnormSig[i / 4];
                normSigForPlot[i + 2] = avgnormSig[i / 4] - errnormSig[i / 4];
                normSigForPlot[i + 3] = avgnormSig[i / 4];
                corrDataCh1ForPlot[i] = avgCorrDataCh1[i / 4];
                corrDataCh1ForPlot[i + 1] = avgCorrDataCh1[i / 4] + errCorrDataCh1[i / 4];
                corrDataCh1ForPlot[i + 2] = avgCorrDataCh1[i / 4] - errCorrDataCh1[i / 4];
                corrDataCh1ForPlot[i + 3] = avgCorrDataCh1[i / 4];
                corrDataCh2ForPlot[i] = avgCorrDataCh2[i / 4];
                corrDataCh2ForPlot[i + 1] = avgCorrDataCh2[i / 4] + errCorrDataCh2[i / 4];
                corrDataCh2ForPlot[i + 2] = avgCorrDataCh2[i / 4] - errCorrDataCh2[i / 4];
                corrDataCh2ForPlot[i + 3] = avgCorrDataCh2[i / 4];
                corrDataDiffForPlot[i] = avgCorrDataDiff[i / 4];
                corrDataDiffForPlot[i + 1] = avgCorrDataDiff[i / 4] + errCorrDataDiff[i / 4];
                corrDataDiffForPlot[i + 2] = avgCorrDataDiff[i / 4] - errCorrDataDiff[i / 4];
                corrDataDiffForPlot[i + 3] = avgCorrDataDiff[i / 4];
                corrDataSumForPlot[i] = avgCorrDataSum[i / 4];
                corrDataSumForPlot[i + 1] = avgCorrDataSum[i / 4] + errCorrDataSum[i / 4];
                corrDataSumForPlot[i + 2] = avgCorrDataSum[i / 4] - errCorrDataSum[i / 4];
                corrDataSumForPlot[i + 3] = avgCorrDataSum[i / 4];
            }
            CameraForm.scatterGraphNormCorrSig.Plots[0].PlotXY(corrbinsForPlot, corrDataCh1ForPlot);
            CameraForm.scatterGraphNormCorrSig.Plots[1].PlotXY(corrbinsForPlot, corrDataCh2ForPlot);
            CameraForm.scatterGraphNormCorrSig.Plots[2].PlotXY(corrbinsForPlot, corrDataDiffForPlot);
            CameraForm.scatterGraphNormCorrSig.Plots[3].PlotXY(corrbinsForPlot, corrDataSumForPlot);
            CameraForm.scatterGraphNormCorrSig.Plots[4].PlotXY(corrbinsForPlot, normSigForPlot);
            

            if (SaveCorrelatorToggle.Value)
            {
                SaveCorrelatorData("correlator\\");
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
            // Retrieve the data from the plot for appending to it and replotting later:
            double[] prevMicromotionDataY = CameraForm.corrMuLog.Plots[0].GetYData();
            double[] prevMicromotionDataX = CameraForm.corrMuLog.Plots[0].GetXData();
            double[] prevAmpDataY = CameraForm.corrAmpLog.Plots[0].GetYData();
            double[] prevAmpDataX = CameraForm.corrAmpLog.Plots[0].GetXData();
            int lastPtIndexMu = prevMicromotionDataX.Length - 1;
            int lastPtIndexAmp = prevAmpDataX.Length - 1;

            // Display current estimated "amplitude" of micromotion or motion suppression on the appropriate graph:
            if (CameraForm.corrRecToggle.Value == false)
            {
                if (LockinFrequencySwitch.Value == true)
                {
                    counterAmp++;
                    /*
                    double dq = Math.Abs(prevAmpDataY[lastPtIndexAmp] - prevAmpDataY[lastPtIndexAmp - 2]);
                    prevAmpDataY[lastPtIndexAmp] = (prevAmpDataY[lastPtIndexAmp] * (counterAmp - 1) + theCorrelator.decompMerit) / counterAmp;
                    prevAmpDataY[lastPtIndexAmp - 3] = prevAmpDataY[lastPtIndexAmp];
                    prevAmpDataY[lastPtIndexAmp - 2] = prevAmpDataY[lastPtIndexAmp] + Math.Sqrt(dq * dq * (counterAmp - 1) * (counterAmp - 1) + Math.Pow(theCorrelator.decompMeritErr, 2)) / counterAmp;
                    prevAmpDataY[lastPtIndexAmp - 1] = prevAmpDataY[lastPtIndexAmp] - Math.Sqrt(dq * dq * (counterAmp - 1) * (counterAmp - 1) + Math.Pow(theCorrelator.decompMeritErr, 2)) / counterAmp;
                     * */
                    prevAmpDataY[lastPtIndexAmp] = normAmplitude;
                    prevAmpDataY[lastPtIndexAmp - 3] = normAmplitude;
                    prevAmpDataY[lastPtIndexAmp - 2] = normAmplitude+normAmplitudeErr;
                    prevAmpDataY[lastPtIndexAmp - 1] = normAmplitude-normAmplitudeErr;

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

//////////////////// Function for calculating statistics of data ////////////////////////////////////////////////////

        private double sampleAve(double[] data, int n)
        {
            double average = 0;
            //int nn = data.Length;
            for (int i = 0; i < n; i++)
                average = average + data[i];
            average = average / n;
            return average;
        }
        private double sampleStd(double[] data, int n)
        {
            double std = 0;
            //int nn = data.Length;
            double ave = sampleAve(data, n);
            for (int i = 0; i < n; i++)
                std = std + Math.Pow(data[i]-ave,2);
            std = Math.Sqrt( std / (n - 1) );
            return std;
        }

////////////////////////////////////////////////////////////////////////////////
        //
        //
        // Correlator thread
        //
        //
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


        //
        //
        // LATTICE POSITION SCAN
        // 
        //

        private void LatticePositionStart_Click(object sender, EventArgs e)
        {
            if (!LatticePositionThreadHelper.ShouldBeRunningFlag)
            {
                LatticePositionThreadHelper.ShouldBeRunningFlag = true;
                LatticePositionThreadHelper.theThread = new Thread(new ThreadStart(LatticePositionExecute));
                LatticePositionThreadHelper.theThread.Name = "Lattice Scan thread";
                LatticePositionThreadHelper.theThread.Priority = ThreadPriority.Normal;
                LatticePositionThreadHelper.index = 0;
                //get scan parameters and declare data arrays
                LatticePositionThreadHelper.numPoints = 3;
                LatticePositionThreadHelper.numAverage = int.Parse(LatticePositionNumAveText.Text);
                //get Stream type from combo box
                LatticePositionThreadHelper.message = LatticePositionComboBox.Text;

                //Get initial slider values
                LatticePositionThreadHelper.KeepDoubles = new double[2];
                LatticePositionThreadHelper.KeepDoubles[0] = (double)this.DCsliders[int.Parse(LatticePositionDC1TextBox.Text)].Value;
                LatticePositionThreadHelper.KeepDoubles[1] = (double)this.DCsliders[int.Parse(LatticePositionDC2TextBox.Text)].Value;

                //define dim 2 array for PMT average and PMT sigma, and for Camera Fluorescence Data
                //if camera is running stop it
                
                LatticePositionThreadHelper.initDoubleData(LatticePositionThreadHelper.numPoints, 2, 2);

                //Compute field scan values, only 3 points here by default
                LatticePositionThreadHelper.DoubleScanVariable[0, 0] = (double)this.DCsliders[int.Parse(LatticePositionDC1TextBox.Text)].Value - double.Parse(LatticePositionAmplitudeText.Text);
                LatticePositionThreadHelper.DoubleScanVariable[0, 1] = (double)this.DCsliders[int.Parse(LatticePositionDC1TextBox.Text)].Value;
                LatticePositionThreadHelper.DoubleScanVariable[0, 2] = (double)this.DCsliders[int.Parse(LatticePositionDC1TextBox.Text)].Value + double.Parse(LatticePositionAmplitudeText.Text);

                LatticePositionThreadHelper.DoubleScanVariable[1, 0] = (double)this.DCsliders[int.Parse(LatticePositionDC2TextBox.Text)].Value + double.Parse(LatticePositionAmplitudeText.Text);
                LatticePositionThreadHelper.DoubleScanVariable[1, 1] = (double)this.DCsliders[int.Parse(LatticePositionDC2TextBox.Text)].Value;
                LatticePositionThreadHelper.DoubleScanVariable[1, 2] = (double)this.DCsliders[int.Parse(LatticePositionDC2TextBox.Text)].Value - double.Parse(LatticePositionAmplitudeText.Text);

                //if ramp array selected
                if (LatticePositionRampArrayCheckbox.Checked)
                {
                    LatticePositionThreadHelper.SingleDouble2 = ArrayTotalSlider.Value;
                    RampSlider(this.ArrayTotalSlider, 100, 10, ArrayTotalSlider.Value, double.Parse(LatticePositionRampArrayValue.Text));
                }

                // if camera or correlator is running stop it
                StopDataStreams();
                

                //start scan thread
                try
                {
                    LatticePositionThreadHelper.theThread.Start();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                LatticePositionThreadHelper.ShouldBeRunningFlag = false;
            }
        }
        private void LatticePositionExecute()
        {
            //update button
            LatticePositionStart.BackColor = System.Drawing.Color.White;
            //clear graph
            CameraForm.ScanResultsGraph.ClearData();
            //if running camera, initialize, and clear fluor and position graphs
            if (LatticePositionThreadHelper.message == "Camera")
            {
                // clear graphs
                CameraForm.FluorescenceGraph.ClearData();
                CameraForm.PositionGraph.ClearData();
                if (Camera.AppInitialize())
                {
                    CameraInitializeHelper();
                }
            }

            if (LatticePositionThreadHelper.message == "Correlator:Sum")
            {
                //Initialize parameters to values entered under "Correlator" Tab  
                //if correlator returns false for init, abort scan
                if (!CorrelatorParameterInit())
                {
                    //end scan
                    LatticePositionThreadHelper.ShouldBeRunningFlag = false;
                    //show message
                    MessageBox.Show("Correlator Init returned false");
                }
            }
            //run scans
            while (LatticePositionThreadHelper.index < (LatticePositionThreadHelper.numPoints) && LatticePositionThreadHelper.ShouldBeRunningFlag)
            { 
                //call to change electrode values
                try
                {
                    this.Invoke(new MyDelegate(LatticePositionFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                if (LatticePositionThreadHelper.message == "Correlator:Sum")
                {
                    for (int i = 0; i < LatticePositionThreadHelper.numAverage; i++)
                    {
                        //Get results into correlator object
                        CorrelatorGetResultsHelper();
                        //Correlator Plots
                        try
                        {
                            this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallbackCh1));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        //get total counts
                        LatticePositionThreadHelper.SingleDouble = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;
                        //load result in array
                        LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index] += LatticePositionThreadHelper.SingleDouble;
                        try
                        {
                            this.Invoke(new MyDelegate(LatticePositionFrmCallback));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //update Sigma array
                        LatticePositionThreadHelper.DoubleData[1, LatticePositionThreadHelper.index] += Math.Pow(LatticePositionThreadHelper.SingleDouble, 2);
                    }
                    //finalize single point average and standard deviation
                    LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index] = LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index] / LatticePositionThreadHelper.numAverage;
                    LatticePositionThreadHelper.DoubleData[1, LatticePositionThreadHelper.index] = Math.Sqrt(LatticePositionThreadHelper.DoubleData[1, LatticePositionThreadHelper.index] / LatticePositionThreadHelper.numAverage - Math.Pow(LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index], 2));

                    lock (LatticePositionThreadHelper)
                    {
                        //display count, plot
                        try
                        {
                            this.BeginInvoke(new MyDelegate(LatticePositionFrmCallback4));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        Monitor.Wait(LatticePositionThreadHelper);
                    }
                    //increase index
                    LatticePositionThreadHelper.index++;
                }
            }

            //Compute figure of merit for centering by taken the difference between the two outer points, normalized by the total fluorescence
            double averageFluor = (double) 1 / 3 * (LatticePositionThreadHelper.DoubleData[0, 0] + LatticePositionThreadHelper.DoubleData[0, 1] + LatticePositionThreadHelper.DoubleData[0, 2]);
            double diff = (LatticePositionThreadHelper.DoubleData[0, 0] - LatticePositionThreadHelper.DoubleData[0, 2]) / averageFluor;

            //Now sample again, but recenter in the direction of decreasing difference with respect to the above normalized difference
            //If looking for a node (switch up) or an antinode (switch down), direction of shift is different
            if (LatticePositionFeedbackSwitch.Value)
            {
                LatticePositionThreadHelper.DoubleScanVariable[0, 0] = LatticePositionThreadHelper.DoubleScanVariable[0, 0] + Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[0, 1] = LatticePositionThreadHelper.DoubleScanVariable[0, 1] + Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[0, 2] = LatticePositionThreadHelper.DoubleScanVariable[0, 2] + Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;

                LatticePositionThreadHelper.DoubleScanVariable[1, 0] = LatticePositionThreadHelper.DoubleScanVariable[1, 0] - Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[1, 1] = LatticePositionThreadHelper.DoubleScanVariable[1, 1] - Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[1, 2] = LatticePositionThreadHelper.DoubleScanVariable[1, 2] - Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
            }
            else
            {
                LatticePositionThreadHelper.DoubleScanVariable[0, 0] = LatticePositionThreadHelper.DoubleScanVariable[0, 0] - Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[0, 1] = LatticePositionThreadHelper.DoubleScanVariable[0, 1] - Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[0, 2] = LatticePositionThreadHelper.DoubleScanVariable[0, 2] - Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;

                LatticePositionThreadHelper.DoubleScanVariable[1, 0] = LatticePositionThreadHelper.DoubleScanVariable[1, 0] + Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[1, 1] = LatticePositionThreadHelper.DoubleScanVariable[1, 1] + Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
                LatticePositionThreadHelper.DoubleScanVariable[1, 2] = LatticePositionThreadHelper.DoubleScanVariable[1, 2] + Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
            }

            //run scans again
            //reset index first
            LatticePositionThreadHelper.index = 0;

            while (LatticePositionThreadHelper.index < (LatticePositionThreadHelper.numPoints) && LatticePositionThreadHelper.ShouldBeRunningFlag)
            {
                //call to change electrode values
                try
                {
                    this.Invoke(new MyDelegate(LatticePositionFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                if (LatticePositionThreadHelper.message == "Correlator:Sum")
                {
                    for (int i = 0; i < LatticePositionThreadHelper.numAverage; i++)
                    {
                        //Get results into correlator object
                        CorrelatorGetResultsHelper();
                        //Correlator Plots
                        try
                        {
                            this.Invoke(new MyDelegate(CorrelatorExecuteFrmCallbackCh1));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //get total counts
                        LatticePositionThreadHelper.SingleDouble = theCorrelator.totalCountsCh1 + theCorrelator.totalCountsCh2;
                        //load result in array
                        LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index] += LatticePositionThreadHelper.SingleDouble;
                        try
                        {
                            this.Invoke(new MyDelegate(LatticePositionFrmCallback));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //update Sigma array
                        LatticePositionThreadHelper.DoubleData[1, LatticePositionThreadHelper.index] += Math.Pow(LatticePositionThreadHelper.SingleDouble, 2);
                    }
                    //finalize single point average and standard deviation
                    LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index] = LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index] / LatticePositionThreadHelper.numAverage;
                    LatticePositionThreadHelper.DoubleData[1, LatticePositionThreadHelper.index] = Math.Sqrt(LatticePositionThreadHelper.DoubleData[1, LatticePositionThreadHelper.index] / LatticePositionThreadHelper.numAverage - Math.Pow(LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index], 2));

                    lock (LatticePositionThreadHelper)
                    {
                        //display count, plot
                        try
                        {
                            this.BeginInvoke(new MyDelegate(LatticePositionFrmCallback4));
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        Monitor.Wait(LatticePositionThreadHelper);
                    }
                    //increase index
                    LatticePositionThreadHelper.index++;
                }
            }

            //Compute figure of merit for centering again
            averageFluor = (double) 1 / 3 * (LatticePositionThreadHelper.DoubleData[0, 0] + LatticePositionThreadHelper.DoubleData[0, 1] + LatticePositionThreadHelper.DoubleData[0, 2]);
            double diff2 = (LatticePositionThreadHelper.DoubleData[0, 0] - LatticePositionThreadHelper.DoubleData[0, 2]) / averageFluor;

            //Now from the two diff values, infer position where diff = 0
            if (LatticePositionFeedbackSwitch.Value)
            {
                LatticePositionThreadHelper.SingleDouble3 = diff / (diff - diff2) * Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
            }
            else { LatticePositionThreadHelper.SingleDouble3 = diff / (diff2 - diff) * Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2; }

            //Reset to original scan variables for center
            LatticePositionThreadHelper.DoubleScanVariable[0, 1] = LatticePositionThreadHelper.DoubleScanVariable[0, 1] - Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;
            LatticePositionThreadHelper.DoubleScanVariable[1, 1] = LatticePositionThreadHelper.DoubleScanVariable[1, 1] + Math.Sign(diff) * double.Parse(LatticePositionAmplitudeText.Text) / 2;

            lock (LatticePositionThreadHelper)
            {
                //reset button and reset to initial slider values
                try
                {
                    this.BeginInvoke(new MyDelegate(LatticePositionFrmCallback2));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
                Monitor.Wait(LatticePositionThreadHelper);
            }

            if (LatticePositionThreadHelper.ShouldBeRunningFlag)
            {
                lock (LatticePositionThreadHelper)
                {
                    //post feedback values
                    try
                    {
                        this.BeginInvoke(new MyDelegate(LatticePositionFrmCallback6));
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    Monitor.Wait(LatticePositionThreadHelper);
                }

            }
            
            //reset scan boolean
            LatticePositionThreadHelper.ShouldBeRunningFlag = false;
        }
        private void LatticePositionFrmCallback()
        {
            //display count
            CameraForm.PMTcountBox.Text = LatticePositionThreadHelper.SingleDouble.ToString();
            //update PMT plot
            CameraForm.PMTcountGraph.PlotYAppend(LatticePositionThreadHelper.SingleDouble);
        }
        private void LatticePositionFrmCallback2()
        {
            lock (LatticePositionThreadHelper)
            {
                LatticePositionStart.BackColor = System.Drawing.Color.Gainsboro;
                LatticePositionStart.Text = "Start *Lattice* Scan";
                //reset to original values
                this.DCsliders[int.Parse(LatticePositionDC1TextBox.Text)].Value = LatticePositionThreadHelper.KeepDoubles[0];
                this.DCsliders[int.Parse(LatticePositionDC2TextBox.Text)].Value = LatticePositionThreadHelper.KeepDoubles[1];
                //update DAC
                compensationAdjustedHelper();
                //if ramp array selected, ramp it back to initial value
                if (LatticePositionRampArrayCheckbox.Checked)
                {
                    RampSlider(ArrayTotalSlider, 100, 10, ArrayTotalSlider.Value, LatticePositionThreadHelper.SingleDouble2);
                }
                Monitor.PulseAll(LatticePositionThreadHelper);
            }
        }
        private void LatticePositionFrmCallback3()
        {
            //Compute new electrode values
            this.DCsliders[int.Parse(LatticePositionDC1TextBox.Text)].Value = LatticePositionThreadHelper.DoubleScanVariable[0, LatticePositionThreadHelper.index];
            this.DCsliders[int.Parse(LatticePositionDC2TextBox.Text)].Value = LatticePositionThreadHelper.DoubleScanVariable[1, LatticePositionThreadHelper.index];
            //update DAC
            compensationAdjustedHelper();
            //Button Indicator
            LatticePositionStart.Text = "Scanning..." + LatticePositionThreadHelper.index.ToString();
        }
        private void LatticePositionFrmCallback4()
        {
            lock (LatticePositionThreadHelper)
            {
                try
                {
                    //plot
                    CameraForm.ScanResultsGraph.PlotXYAppend(LatticePositionThreadHelper.DoubleScanVariable[0, LatticePositionThreadHelper.index], LatticePositionThreadHelper.DoubleData[0, LatticePositionThreadHelper.index]);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                Monitor.PulseAll(LatticePositionThreadHelper);
            }
        }

        private void LatticePositionFrmCallback6()
        {
            lock (LatticePositionThreadHelper)
            {
                //post feedback values
                LatticePositionFeedbackText.Text = LatticePositionThreadHelper.SingleDouble3.ToString("F3");
                double DC1 = this.DCsliders[int.Parse(LatticePositionDC1TextBox.Text)].Value + LatticePositionThreadHelper.SingleDouble3;
                double DC2 = this.DCsliders[int.Parse(LatticePositionDC2TextBox.Text)].Value - LatticePositionThreadHelper.SingleDouble3;
                LatticePositionNewValueText.Text = DC1.ToString("F3");
                LatticePositionNewValue2Text.Text = DC2.ToString("F3");
                Monitor.PulseAll(LatticePositionThreadHelper);
            }
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
                ElectrodeScanThreadHelper.KeepDoubles = new double[2];
                ElectrodeScanThreadHelper.KeepDoubles[0] = this.DCsliders[int.Parse(ElectrodeScanDC1TextBox.Text)].Value;
                ElectrodeScanThreadHelper.KeepDoubles[1] = this.DCsliders[int.Parse(ElectrodeScanDC2TextBox.Text)].Value;
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
                if (ElectrodeScanThreadHelper.message == "PMT")
                {
                    ElectrodeScanThreadHelper.initDoubleData(ElectrodeScanThreadHelper.numPoints, 2, 2);
                }
                else
                {
                    ElectrodeScanThreadHelper.initDoubleData(ElectrodeScanThreadHelper.numPoints, 1, 2);
                    // if camera or correlator is running stop it
                    StopDataStreams();
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
            CameraForm.ScanResultsGraph.ClearData();
            //initialize
            initializeDataStreams(ElectrodeScanThreadHelper);
            //run scans
            while (ElectrodeScanThreadHelper.index < (ElectrodeScanThreadHelper.numPoints) && ElectrodeScanThreadHelper.ShouldBeRunningFlag)
            {
                //Compute new field values
                ElectrodeScanThreadHelper.DoubleScanVariable[0,ElectrodeScanThreadHelper.index] = (double)(ElectrodeScanThreadHelper.min[0] + (ElectrodeScanThreadHelper.max[0] - ElectrodeScanThreadHelper.min[0]) * ElectrodeScanThreadHelper.index / (ElectrodeScanThreadHelper.numPoints - 1));
                ElectrodeScanThreadHelper.DoubleScanVariable[1,ElectrodeScanThreadHelper.index] = (double)(ElectrodeScanThreadHelper.min[1] + (ElectrodeScanThreadHelper.max[1] - ElectrodeScanThreadHelper.min[1]) * ElectrodeScanThreadHelper.index / (ElectrodeScanThreadHelper.numPoints - 1));
                //call to change electrode values
                try
                {
                    this.Invoke(new MyDelegate(ElectrodeScanFrmCallback3));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
                //get data
                getDatafromStream(ElectrodeScanThreadHelper);
                //index++
                ElectrodeScanThreadHelper.index++;
            }
            if (ElectrodeScanThreadHelper.ShouldBeRunningFlag && ElectrodeScanSaveCheckbox.Checked)
            {
                //save Scan Data
                SaveScanData(ElectrodeScanThreadHelper);
            }
            //reset button and go back to initial values
            try
            {
                this.BeginInvoke(new MyDelegate(ElectrodeScanFrmCallback2));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            //reset scan boolean
            ElectrodeScanThreadHelper.ShouldBeRunningFlag = false;
        }

        private void ElectrodeScanFrmCallback2()
        {
            ElectrodeScanStart.BackColor = System.Drawing.Color.Gainsboro;
            ElectrodeScanStart.Text = "Start *Electrode* Scan";
            //reset to original values
            this.DCsliders[int.Parse(ElectrodeScanDC1TextBox.Text)].Value = ElectrodeScanThreadHelper.KeepDoubles[0];
            this.DCsliders[int.Parse(ElectrodeScanDC2TextBox.Text)].Value = ElectrodeScanThreadHelper.KeepDoubles[1];
            //update DAC
            compensationAdjustedHelper();
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
                SliderScanThreadHelper.numScanVar = 1;
                //get scan parameters and declare data arrays
                SliderScanThreadHelper.min = new double[1];
                SliderScanThreadHelper.max = new double[1];
                SliderScanThreadHelper.min[0] = double.Parse(SliderScanStartValueTextbox.Text);
                SliderScanThreadHelper.max[0] = double.Parse(SliderScanEndValueTextbox.Text);
                SliderScanThreadHelper.numAverage = int.Parse(SliderScanPMTAveragingTextbox.Text);
                SliderScanThreadHelper.numPoints = int.Parse(SliderScanNumPointsTextbox.Text);
                SliderScanThreadHelper.theButton = SliderScanStart;
                //get Slider to scan
                SliderScanThreadHelper.theSlider = getSliderfromText(SliderScanSelection.Text);
                //Keep initial slider value
                SliderScanThreadHelper.KeepDoubles = new double[1];
                SliderScanThreadHelper.KeepDoubles[0] = SliderScanThreadHelper.theSlider.Value;
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
                if (SliderScanThreadHelper.message == "PMT")
                {
                    SliderScanThreadHelper.initDoubleData(SliderScanThreadHelper.numPoints, 2, 1);
                }
                else
                {
                    SliderScanThreadHelper.initDoubleData(SliderScanThreadHelper.numPoints, 1, 1);
                    // if camera or correlator is running stop it
                    StopDataStreams();
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
            CameraForm.ScanResultsGraph.ClearData();
            //init data stream
            initializeDataStreams(SliderScanThreadHelper);

            //run scans
            while (SliderScanThreadHelper.index < (SliderScanThreadHelper.numPoints) && SliderScanThreadHelper.ShouldBeRunningFlag)
            {
                //Compute new field values
                SliderScanThreadHelper.DoubleScanVariable[0, SliderScanThreadHelper.index] = (double)(SliderScanThreadHelper.min[0] + (SliderScanThreadHelper.max[0] - SliderScanThreadHelper.min[0]) * SliderScanThreadHelper.index / (SliderScanThreadHelper.numPoints - 1));
                //call to change field value
                try
                {
                    this.Invoke(new MyDelegateThreadHelper(ScanUpdateCallbackFn), SliderScanThreadHelper);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                //get data
                getDatafromStream(SliderScanThreadHelper);
                //index++
                SliderScanThreadHelper.index++;
            }
            if (SliderScanThreadHelper.ShouldBeRunningFlag && SliderScanSaveCheckbox.Checked)
            {
                //save Scan Data
                SaveScanData(SliderScanThreadHelper);
            }
            //go back to initial value
            //reset button
            try
            {
                this.Invoke(new MyDelegateThreadHelper(ScanResetCallbackFn), SliderScanThreadHelper);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            //reset scan boolean
            SliderScanThreadHelper.ShouldBeRunningFlag = false;
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
                    // if camera or correlator is running stop it
                    StopDataStreams();
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
            CameraForm.ScanResultsGraph.ClearData();
            //initialize data stream
            initializeDataStreams(FluorLogThreadHelper);
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

                //get data
                getDatafromStream(FluorLogThreadHelper);
                //index++
                FluorLogThreadHelper.index++;
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
            double multiplier = double.Parse(BackgroundMultiplierTextbox.Text);
            //center of mass variable
            double position = 0;
            
            //variables for ion focus optimization, indices of max fluorescence pixel
            int xmax = 0, ymax = 0;
            double fmax = 0;
            double background = 0;

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
                    background = CameraThreadHelper.DoubleData[x1, y1];

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
                            //find background
                            if (background > CameraThreadHelper.DoubleData[j, k])
                            {
                                background = CameraThreadHelper.DoubleData[j, k];
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
                        CameraThreadHelper.DoubleArray[1] = (CameraThreadHelper.DoubleData[xmax - 1, ymax] + CameraThreadHelper.DoubleData[xmax + 1, ymax] - 2*background) / (CameraThreadHelper.DoubleData[xmax - 1, ymax] + CameraThreadHelper.DoubleData[xmax + 1, ymax] + CameraThreadHelper.DoubleData[xmax, ymax] - 3*background);
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
                        CameraThreadHelper.DoubleArray[3] = (CameraThreadHelper.DoubleData[xmax, ymax - 1] + CameraThreadHelper.DoubleData[xmax, ymax + 1] - 2*background) / (CameraThreadHelper.DoubleData[xmax, ymax - 1] + CameraThreadHelper.DoubleData[xmax, ymax + 1] + CameraThreadHelper.DoubleData[xmax, ymax] - 3*background);
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
            CameraForm.xBalanceGraph.Plots[1].PlotYAppend(0);
        }
        private void CameraFormThreadCallBack4()
        {
            CameraForm.xSpreadGraph.PlotYAppend(CameraThreadHelper.DoubleArray[1]);
            CameraForm.xSpreadGraph.Plots[1].PlotYAppend(0);
        }
        private void CameraFormThreadCallBack5()
        {
            CameraForm.yBalanceGraph.PlotYAppend(CameraThreadHelper.DoubleArray[2]);
            CameraForm.yBalanceGraph.Plots[1].PlotYAppend(0);
        }
        private void CameraFormThreadCallBack6()
        {
            CameraForm.ySpreadGraph.PlotYAppend(CameraThreadHelper.DoubleArray[3]);
            CameraForm.ySpreadGraph.Plots[1].PlotYAppend(0);
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
            richTextBox1.Text = Camera.sStatusMsg;
        }
        private void CameraThreadFrmCallback2()
        {
            //update message status box
            richTextBox1.Text = Camera.sStatusMsg;
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
            BackgroundTextbox.Text = CameraThreadHelper.Background.ToString("F2");
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
            CameraThreadHelper.message = BackgroundComboBox.Text;
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
            richTextBox1.Text = "Attempting Camera Initialization...";

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
        { }

        private void switchDisplayBases_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (switchDisplayBases.Value)
            {
                CameraForm.PMTcountGraph.Plots[0].Visible = true;
                CameraForm.PMTcountGraph.Plots[1].Visible = true;
                CameraForm.PMTcountGraph.Plots[2].Visible = false;
                CameraForm.PMTcountGraph.Plots[3].Visible = false;
            }
            else
            {
                CameraForm.PMTcountGraph.Plots[0].Visible = false;
                CameraForm.PMTcountGraph.Plots[1].Visible = false;
                CameraForm.PMTcountGraph.Plots[2].Visible = true;
                CameraForm.PMTcountGraph.Plots[3].Visible = true;
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
            syncTimeScales(lockinper1Val);
        }
        private void LockInPertext1_TextChanged(object sender, EventArgs e)
        {
            double lockinper1Val = Double.Parse(LockInPertext1.Text); // in kHz
            double lockinfreq1Val = 1 / lockinper1Val * 1000; // in microseconds
            LockInFreqtext1.Text = lockinfreq1Val.ToString();
            syncTimeScales(lockinper1Val);
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
            pulsePeriodVal = Double.Parse(pulsePeriodText.Text); // in microseconds
            double pulseFreqVal = 1 / pulsePeriodVal * 1000; // in kHz
            pulseFreqLabel.Text = pulseFreqVal.ToString();
            syncTimeScales(pulsePeriodVal);
            refreshChannelsHelper();
        }

        private void slow_pulsePeriodText_TextChanged(object sender, EventArgs e)
        {
            syncTimeScales(pulsePeriodVal);
        }

        private void syncTimeScales(double mainref)
        {
            pulsePeriodVal = mainref;
            pulsePeriodText.Text = pulsePeriodVal.ToString();
            double pulseFreqVal = 1 / pulsePeriodVal * 1000; // in kHz
            pulseFreqLabel.Text = pulseFreqVal.ToString();
            slow_pulsePeriodVal = Double.Parse(slow_pulsePeriodText.Text); // in microseconds
            double slow_pulsePeriodValUS = slow_pulsePeriodVal * pulsePeriodVal; // in kHz
            double slow_pulseFreqVal = 1 / slow_pulsePeriodValUS * 1000; // in kHz
            slowUS_pulsePeriodText.Text = slow_pulsePeriodValUS.ToString();
            slow_pulseFreqLabel.Text = slow_pulseFreqVal.ToString();
            refreshChannelsHelper();
        }
        ///////////////////////////////////////////////////////////

        private void updateAllSignalsButton_Click(object sender, EventArgs e)
        {
            /*
            theCorrelator.PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(pulsePeriodText.Text), 0));

            theCorrelator.onTimeOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1OnTimeText.Text), 0));
            theCorrelator.onTimeOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2OnTimeText.Text), 0));
            theCorrelator.onTimeOut[2] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out3OnTimeText.Text), 0));
            theCorrelator.onTimeOut[3] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out4OnTimeText.Text), 0));

            theCorrelator.delayOut[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out1DelayText.Text), 0));
            theCorrelator.delayOut[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out2DelayText.Text), 0));
            theCorrelator.delayOut[2] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out3DelayText.Text), 0));
            theCorrelator.delayOut[3] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(out4DelayText.Text), 0));

            theCorrelator.onTimeIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1OnTimeText.Text), 0));
            theCorrelator.onTimeIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2OnTimeText.Text), 0));
            theCorrelator.delayIn[0] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in1DelayText.Text), 0));
            theCorrelator.delayIn[1] = (uint)(Math.Round(theCorrelator.ok.P * double.Parse(in2DelayText.Text), 0));


            theCorrelator.slow_PulseClkDiv = uint.Parse(slow_pulsePeriodText.Text);

            theCorrelator.slow_onTimeOut[0] = uint.Parse(slow_out1OnTimeText.Text);
            theCorrelator.slow_onTimeOut[1] = uint.Parse(slow_out2OnTimeText.Text);
            theCorrelator.slow_onTimeOut[2] = uint.Parse(slow_out3OnTimeText.Text);
            theCorrelator.slow_onTimeOut[3] = uint.Parse(slow_out4OnTimeText.Text);

            theCorrelator.slow_delayOut[0] = uint.Parse(slow_out1DelayText.Text);
            theCorrelator.slow_delayOut[1] = uint.Parse(slow_out2DelayText.Text);
            theCorrelator.slow_delayOut[2] = uint.Parse(slow_out3DelayText.Text);
            theCorrelator.slow_delayOut[3] = uint.Parse(slow_out4DelayText.Text);

            theCorrelator.slow_onTimeIn[0] = uint.Parse(slow_in1OnTimeText.Text);
            theCorrelator.slow_onTimeIn[1] = uint.Parse(slow_in2OnTimeText.Text);
            theCorrelator.slow_delayIn[0] = uint.Parse(slow_in1DelayText.Text);
            theCorrelator.slow_delayIn[1] = uint.Parse(slow_in2DelayText.Text);
            */

            theCorrelator.PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * pulsePeriodVal, 0));
            theCorrelator.slow_PulseClkDiv = (uint)(Math.Round(theCorrelator.ok.P * slow_pulsePeriodVal, 0));

            for (int i = 0; i < nfastchOut; i++)
            {
                theCorrelator.onTimeOut[i] = (uint)(Math.Round(theCorrelator.ok.P * fastOutCh[i].Param2Value, 0));
                theCorrelator.delayOut[i] = (uint)(Math.Round(theCorrelator.ok.P * fastOutCh[i].Param3Value, 0));
                theCorrelator.subperiodOut[i] = (uint)(Math.Round(theCorrelator.ok.P * fastOutCh[i].Param1Value, 0));
            }
            for (int i = 0; i < nfastchIn; i++)
            {
                theCorrelator.onTimeIn[i] = (uint)(Math.Round(theCorrelator.ok.P * fastInCh[i].Param2Value, 0));
                theCorrelator.delayIn[i] = (uint)(Math.Round(theCorrelator.ok.P * fastInCh[i].Param3Value, 0));
                theCorrelator.subperiodIn[i] = (uint)(Math.Round(theCorrelator.ok.P * fastInCh[i].Param1Value, 0));
            }
            for (int i = 0; i < nslowchOut; i++)
            {
                theCorrelator.slow_onTimeOut[i] = (uint)(slowOutCh[i].Param2Value);
                theCorrelator.slow_delayOut[i] = (uint)(slowOutCh[i].Param3Value);
                theCorrelator.slow_subperiodOut[i] = (uint)(slowOutCh[i].Param1Value);
            }
            for (int i = 0; i < nslowchIn; i++)
            {
                theCorrelator.slow_onTimeIn[i] = (uint)(slowInCh[i].Param2Value);
                theCorrelator.slow_delayIn[i] = (uint)(slowInCh[i].Param3Value);
                theCorrelator.slow_subperiodIn[i] = (uint)(slowInCh[i].Param1Value);
            }

            theCorrelator.updateAllSignalsLive();

        }

        private void syncSrcSw_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (syncSrcSw.Value) { theCorrelator.syncSrcChoose = true; }
            else { theCorrelator.syncSrcChoose = false; }

            theCorrelator.updateSyncSourceLive();
        }

        private void recaplock_switch_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (recaplock_switch.Value) { theCorrelator.recaplockStatus = true; }
            else { theCorrelator.recaplockStatus = false; }

            theCorrelator.updateRecaplockStatusLive();
        }

        private void recaplockTheshold_TextChanged(object sender, EventArgs e)
        {
            theCorrelator.recaplockThreshold = uint.Parse(recaplock_threshold_textbox.Text);
            theCorrelator.updateRecaplockThresholdLive();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            double temp = Math.Round(double.Parse(recaplock_thresholdfrac_textbox.Text) * double.Parse(recaplockcounts_display.Text));
            recaplock_threshold_textbox.Text = temp.ToString();
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

        ////////////Fast input ch1 options //////////////////
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

        ////////////Fast input ch2 options//////////////////
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
        /*
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
        private void slow_pulsePeriodText_TextChanged1(object sender, EventArgs e)
        {
            if (notc1so2.Checked)
            {
                double vaux2 = double.Parse(slow_out1DelayText.Text) + double.Parse(slow_out1OnTimeText.Text);
                double vaux1 = double.Parse(slow_pulsePeriodText.Text) - vaux2;

                slow_out2OnTimeText.Text = vaux1.ToString();
                slow_out2DelayText.Text = vaux2.ToString();
            }
        }
         * */
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

       ///////////////////////////////////



        ////////
        ////////EXPERIMENTAL SEQUENCER CODE
        ////////

        private void ExpSeqStartButton_Click(object sender, EventArgs e)
        {
            if (!ExperimentalSequencerThreadHelper.ShouldBeRunningFlag)
            {
                ExperimentalSequencerThreadHelper.ShouldBeRunningFlag = true;
                ExperimentalSequencerThreadHelper.theThread = new Thread(new ThreadStart(ExperimentalSequencerExecute));
                ExperimentalSequencerThreadHelper.theThread.Name = "Slider Scan thread";
                ExperimentalSequencerThreadHelper.theThread.Priority = ThreadPriority.Normal;
                ExperimentalSequencerThreadHelper.index = 0;
                ExperimentalSequencerThreadHelper.index2 = 0;
                if (ExpSeqMainSlider2.Text == "None") { ExperimentalSequencerThreadHelper.numScanVar = 1; }
                else { ExperimentalSequencerThreadHelper.numScanVar = 2; }
                //get scan parameters and declare data arrays
                ExperimentalSequencerThreadHelper.min = new double[ExperimentalSequencerThreadHelper.numScanVar];
                ExperimentalSequencerThreadHelper.max = new double[ExperimentalSequencerThreadHelper.numScanVar];
                ExperimentalSequencerThreadHelper.min[0] = double.Parse(ExpSeqMainSliderStartText.Text);
                ExperimentalSequencerThreadHelper.max[0] = double.Parse(ExpSeqMainSliderEndText.Text);
                if (ExperimentalSequencerThreadHelper.numScanVar > 1)
                {
                    ExperimentalSequencerThreadHelper.min[1] = double.Parse(ExpSeqMainSlider2StartText.Text);
                    ExperimentalSequencerThreadHelper.max[1] = double.Parse(ExpSeqMainSlider2EndText.Text);
                }
                ExperimentalSequencerThreadHelper.numAverage = int.Parse(ExpSeqMainSliderNumScansText.Text);
                ExperimentalSequencerThreadHelper.numPoints = int.Parse(ExpSeqMainSliderNumPointsText.Text);
                ExperimentalSequencerThreadHelper.theButton = ExpSeqStartButton;
                ExperimentalSequencerThreadHelper.slider1Name = ExpSeqMainSlider1.Text;
                ExperimentalSequencerThreadHelper.slider2Name = ExpSeqMainSlider2.Text;
                ExperimentalSequencerThreadHelper.folderPathExtra = "ExpSeq\\" + ExpSeqExtraPathnameText.Text;
                //get Slider to scan
                ExperimentalSequencerThreadHelper.theSlider = getSliderfromText(ExperimentalSequencerThreadHelper.slider1Name);
                ExperimentalSequencerThreadHelper.theSlider2 = getSliderfromText(ExperimentalSequencerThreadHelper.slider2Name);
                //Keep initial slider value
                ExperimentalSequencerThreadHelper.KeepDoubles = new double[ExperimentalSequencerThreadHelper.numScanVar];
                ExperimentalSequencerThreadHelper.KeepDoubles[0] = ExperimentalSequencerThreadHelper.theSlider.Value;
                if (ExperimentalSequencerThreadHelper.numScanVar > 1)
                {
                    ExperimentalSequencerThreadHelper.KeepDoubles[1] = ExperimentalSequencerThreadHelper.theSlider2.Value;
                }
                //modify thread name for file saving
                ExperimentalSequencerThreadHelper.threadName = ExperimentalSequencerThreadHelper.theSlider.Name;
                if (ExperimentalSequencerThreadHelper.numScanVar > 1)
                {
                    ExperimentalSequencerThreadHelper.threadName += ExperimentalSequencerThreadHelper.theSlider2.Name;    
                }

                if (ExperimentalSequencerThreadHelper.numPoints < 1)
                {
                    ExperimentalSequencerThreadHelper.numPoints = 1;
                    ExpSeqMainSliderNumPointsText.Text = "1";
                }
                //get Stream type from combo box
                ExperimentalSequencerThreadHelper.message = ExpSeqMainData.Text;

                //define dim 2 array for PMT average and PMT sigma, and for Camera Fluorescence Data
                if (ExperimentalSequencerThreadHelper.message == "PMT")
                {
                    ExperimentalSequencerThreadHelper.initDoubleData(ExperimentalSequencerThreadHelper.numPoints, 2, ExperimentalSequencerThreadHelper.numScanVar);
                }
                else
                {
                    ExperimentalSequencerThreadHelper.initDoubleData(ExperimentalSequencerThreadHelper.numPoints, 1, ExperimentalSequencerThreadHelper.numScanVar);

                    // if camera or correlator is running stop it
                    StopDataStreams();
                }

                //if Interlocked Scan 1 checked, initialize thread helper
                if (ExpSeqScan1Checkbox.Checked)
                {
                    InterlockedScan1ThreadHelper.ShouldBeRunningFlag = true;
                    InterlockedScan1ThreadHelper.index = 0;
                    if (ExpSeqInt1Slider2.Text == "None") { InterlockedScan1ThreadHelper.numScanVar = 1; }
                    else { InterlockedScan1ThreadHelper.numScanVar = 2; }
                    //get scan parameters and declare data arrays
                    InterlockedScan1ThreadHelper.min = new double[InterlockedScan1ThreadHelper.numScanVar];
                    InterlockedScan1ThreadHelper.max = new double[InterlockedScan1ThreadHelper.numScanVar];
                    InterlockedScan1ThreadHelper.min[0] = double.Parse(ExpSeqInt1Slider1StartText.Text);
                    InterlockedScan1ThreadHelper.max[0] = double.Parse(ExpSeqInt1Slider1StopText.Text);
                    if (InterlockedScan1ThreadHelper.numScanVar > 1)
                    {
                        InterlockedScan1ThreadHelper.min[1] = double.Parse(ExpSeqInt1Slider2StartText.Text);
                        InterlockedScan1ThreadHelper.max[1] = double.Parse(ExpSeqInt1Slider2StopText.Text);
                    }

                    InterlockedScan1ThreadHelper.numPoints = int.Parse(ExpSeqIntSlider1NumPointsText.Text);

                    InterlockedScan1ThreadHelper.folderPathExtra = "ExpSeq\\" + ExpSeqExtraPathnameText.Text + "IntScan1\\";
                    //get Slider to scan
                    InterlockedScan1ThreadHelper.theSlider = getSliderfromText(ExpSeqInt1Slider1.Text);
                    InterlockedScan1ThreadHelper.theSlider2 = getSliderfromText(ExpSeqInt1Slider2.Text);


                    //Keep initial slider value
                    InterlockedScan1ThreadHelper.KeepDoubles = new double[InterlockedScan1ThreadHelper.numScanVar];
                    InterlockedScan1ThreadHelper.KeepDoubles[0] = InterlockedScan1ThreadHelper.theSlider.Value;
                    if (InterlockedScan1ThreadHelper.numScanVar > 1)
                    {
                        InterlockedScan1ThreadHelper.KeepDoubles[1] = InterlockedScan1ThreadHelper.theSlider2.Value;
                    }
                    //modify thread name for file saving
                    InterlockedScan1ThreadHelper.threadName = InterlockedScan1ThreadHelper.theSlider.Name;

                    if (InterlockedScan1ThreadHelper.numPoints < 1)
                    {
                        InterlockedScan1ThreadHelper.numPoints = 1;
                        ExpSeqIntSlider1NumPointsText.Text = "1";
                    }
                    //get Stream type from combo box
                    InterlockedScan1ThreadHelper.message = ExpSeqInt1DataSream.Text;
                    InterlockedScan1ThreadHelper.initDoubleData(InterlockedScan1ThreadHelper.numPoints, 1, InterlockedScan1ThreadHelper.numScanVar);

                }

                if (ExpSeqScan2Checkbox.Checked)
                {
                    InterlockedScan2ThreadHelper.ShouldBeRunningFlag = true;
                    InterlockedScan2ThreadHelper.index = 0;
                    if (ExpSeqInt2Slider2.Text == "None") { InterlockedScan2ThreadHelper.numScanVar = 1; }
                    else { InterlockedScan2ThreadHelper.numScanVar = 2; }
                    //get scan parameters and declare data arrays
                    InterlockedScan2ThreadHelper.min = new double[InterlockedScan2ThreadHelper.numScanVar];
                    InterlockedScan2ThreadHelper.max = new double[InterlockedScan2ThreadHelper.numScanVar];
                    InterlockedScan2ThreadHelper.min[0] = double.Parse(ExpSeqInt2Slider1StartText.Text);
                    InterlockedScan2ThreadHelper.max[0] = double.Parse(ExpSeqInt2Slider1StopText.Text);
                    if (InterlockedScan2ThreadHelper.numScanVar > 1)
                    {
                        InterlockedScan2ThreadHelper.min[1] = double.Parse(ExpSeqInt2Slider2StartText.Text);
                        InterlockedScan2ThreadHelper.max[1] = double.Parse(ExpSeqInt2Slider2StopText.Text);
                    }
                    InterlockedScan2ThreadHelper.numPoints = int.Parse(ExpSeqIntSlider2NumPointsText.Text);

                    InterlockedScan2ThreadHelper.folderPathExtra = "ExpSeq\\" + ExpSeqExtraPathnameText.Text + "IntScan2\\";
                    //get Slider to scan
                    InterlockedScan2ThreadHelper.theSlider = getSliderfromText(ExpSeqInt2Slider1.Text);
                    InterlockedScan2ThreadHelper.theSlider2 = getSliderfromText(ExpSeqInt2Slider2.Text);


                    //Keep initial slider value
                    InterlockedScan2ThreadHelper.KeepDoubles = new double[InterlockedScan2ThreadHelper.numScanVar];
                    InterlockedScan2ThreadHelper.KeepDoubles[0] = InterlockedScan2ThreadHelper.theSlider.Value;
                    if (InterlockedScan2ThreadHelper.numScanVar > 1)
                    {
                        InterlockedScan2ThreadHelper.KeepDoubles[1] = InterlockedScan2ThreadHelper.theSlider2.Value;
                    }
                    //modify thread name for file saving
                    InterlockedScan2ThreadHelper.threadName = InterlockedScan2ThreadHelper.theSlider.Name;

                    if (InterlockedScan2ThreadHelper.numPoints < 1)
                    {
                        InterlockedScan2ThreadHelper.numPoints = 1;
                        ExpSeqIntSlider2NumPointsText.Text = "1";
                    }
                    //get Stream type from combo box
                    InterlockedScan2ThreadHelper.message = ExpSeqInt2DataSream.Text;
                    InterlockedScan2ThreadHelper.initDoubleData(InterlockedScan2ThreadHelper.numPoints, 1, InterlockedScan2ThreadHelper.numScanVar);

                }

                //start scan thread
                try
                {
                    ExperimentalSequencerThreadHelper.theThread.Start();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                ExperimentalSequencerThreadHelper.ShouldBeRunningFlag = false;
                InterlockedScan1ThreadHelper.ShouldBeRunningFlag = false;
                InterlockedScan2ThreadHelper.ShouldBeRunningFlag = false;
            }

        }

        private void ExperimentalSequencerExecute()
        {
            //update button
            ExpSeqStartButton.BackColor = System.Drawing.Color.White;
            //clear graph
            CameraForm.ScanResultsGraph.ClearData();
            //initialize camera or correlator depending on desired data stream
            initializeDataStreams(ExperimentalSequencerThreadHelper);

            //loop scans
            while (ExperimentalSequencerThreadHelper.index2 < (ExperimentalSequencerThreadHelper.numAverage) && ExperimentalSequencerThreadHelper.ShouldBeRunningFlag)
            {
                InterlockedScan1ThreadHelper.ShouldBeRunningFlag = true;
                InterlockedScan2ThreadHelper.ShouldBeRunningFlag = true;

                ExperimentalSequencerThreadHelper.index = 0;
                
                //update label
                int updateInt = ExperimentalSequencerThreadHelper.index2 + 1;
                string update = "Scan # " + updateInt.ToString();
                try
                {
                    this.Invoke(new MyDelegateLabelUpdate(UpdateLabelCallbackFn), update, ExpSeqScanNumberLabel);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

                //run scans
                while (ExperimentalSequencerThreadHelper.index < (ExperimentalSequencerThreadHelper.numPoints) && ExperimentalSequencerThreadHelper.ShouldBeRunningFlag)
                {
                    //Compute new field values
                    if (ExperimentalSequencerThreadHelper.numPoints > 1) { ExperimentalSequencerThreadHelper.DoubleScanVariable[0, ExperimentalSequencerThreadHelper.index] = (double)(ExperimentalSequencerThreadHelper.min[0] + (ExperimentalSequencerThreadHelper.max[0] - ExperimentalSequencerThreadHelper.min[0]) * ExperimentalSequencerThreadHelper.index / (ExperimentalSequencerThreadHelper.numPoints - 1)); }
                    else { ExperimentalSequencerThreadHelper.DoubleScanVariable[0, ExperimentalSequencerThreadHelper.index] = ExperimentalSequencerThreadHelper.min[0]; }

                    if (ExperimentalSequencerThreadHelper.numScanVar > 1)
                    {
                        if (ExperimentalSequencerThreadHelper.numPoints > 1) { ExperimentalSequencerThreadHelper.DoubleScanVariable[1, ExperimentalSequencerThreadHelper.index] = (double)(ExperimentalSequencerThreadHelper.min[1] + (ExperimentalSequencerThreadHelper.max[1] - ExperimentalSequencerThreadHelper.min[1]) * ExperimentalSequencerThreadHelper.index / (ExperimentalSequencerThreadHelper.numPoints - 1)); }
                        else {ExperimentalSequencerThreadHelper.DoubleScanVariable[1, ExperimentalSequencerThreadHelper.index] = ExperimentalSequencerThreadHelper.min[1];}
                    }
                    //call to change field value
                    try
                    {
                        this.Invoke(new MyDelegateThreadHelper(ScanUpdateCallbackFn),ExperimentalSequencerThreadHelper);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }

                    //if first scan, pause for 500ms
                    if (ExperimentalSequencerThreadHelper.index == 0)
                    {
                        //wait for 500ms before scanning
                        Thread.Sleep(int.Parse(ExpSeqTimeDelay.Text));
                    }

                    //get Data from selected Data Stream into ThreadHelper variable
                    //update graphs
                    getDatafromStream(ExperimentalSequencerThreadHelper);

                    if(ExperimentalSequencerThreadHelper.message == "Correlator:Channels")
                    {
                        if(ExperimentalSequencerThreadHelper.DoubleDataArray == null || ExperimentalSequencerThreadHelper.index2 == 0)
                        {
                            ExperimentalSequencerThreadHelper.DoubleDataArray = new double[8][,];
                            for (int i = 0; i < 8; i++)
                            {
                                ExperimentalSequencerThreadHelper.DoubleDataArray[i] = new double[ExperimentalSequencerThreadHelper.numPoints, theCorrelator.phcountarrayCh1.Length];
                            }

                            for(int i = 0; i< ExperimentalSequencerThreadHelper.numPoints;i++)
                            {
                                for(int j = 0; j< theCorrelator.phcountarrayCh1.Length; j++)
                                {
                                    for (int k = 0; k < 8; k++)
                                    {
                                        ExperimentalSequencerThreadHelper.DoubleDataArray[k][i, j] = 0;
                                    }
                                }
                            }
                        }

                        for (int i = 0; i < theCorrelator.phcountarrayCh1.Length; i++)
                        {
                            //PMT1, PMT2, SUM, DIFFERENCE1
                            double newCh1 = (ExperimentalSequencerThreadHelper.DoubleDataArray[0][ExperimentalSequencerThreadHelper.index, i] * (ExperimentalSequencerThreadHelper.index2) + theCorrelator.phcountarrayCh1[i]) / (ExperimentalSequencerThreadHelper.index2 + 1);
                            double newCh2 = (ExperimentalSequencerThreadHelper.DoubleDataArray[1][ExperimentalSequencerThreadHelper.index, i] * (ExperimentalSequencerThreadHelper.index2) + theCorrelator.phcountarrayCh2[i]) / (ExperimentalSequencerThreadHelper.index2 + 1);
                            double instSum = theCorrelator.phcountarrayCh1[i] + theCorrelator.phcountarrayCh2[i];
                            double instDiff = (theCorrelator.phcountarrayCh1[i] - theCorrelator.phcountarrayCh2[i]) / (theCorrelator.phcountarrayCh1[i] + theCorrelator.phcountarrayCh2[i]);
                            double newSum = newCh1 + newCh2;
                            double newDiff = (newCh1 - newCh2) / (newCh1 + newCh2);

                            double newCh1Err = Math.Sqrt(Math.Pow(ExperimentalSequencerThreadHelper.DoubleDataArray[4][ExperimentalSequencerThreadHelper.index, i],2) * (ExperimentalSequencerThreadHelper.index2) + Math.Pow(theCorrelator.phcountarrayCh1[i] - newCh1,2)) / (ExperimentalSequencerThreadHelper.index2 + 1);
                            double newCh2Err = Math.Sqrt(Math.Pow(ExperimentalSequencerThreadHelper.DoubleDataArray[5][ExperimentalSequencerThreadHelper.index, i], 2) * (ExperimentalSequencerThreadHelper.index2) + Math.Pow(theCorrelator.phcountarrayCh2[i] - newCh1, 2)) / (ExperimentalSequencerThreadHelper.index2 + 1);
                            double newSumErr = Math.Sqrt(Math.Pow(ExperimentalSequencerThreadHelper.DoubleDataArray[6][ExperimentalSequencerThreadHelper.index, i], 2) * (ExperimentalSequencerThreadHelper.index2) + Math.Pow(newSum - instSum, 2)) / (ExperimentalSequencerThreadHelper.index2 + 1);
                            double newDiffErr = Math.Sqrt(Math.Pow(ExperimentalSequencerThreadHelper.DoubleDataArray[7][ExperimentalSequencerThreadHelper.index, i], 2) * (ExperimentalSequencerThreadHelper.index2) + Math.Pow(newDiff - instDiff, 2)) / (ExperimentalSequencerThreadHelper.index2 + 1);
                            
                            ExperimentalSequencerThreadHelper.DoubleDataArray[0][ExperimentalSequencerThreadHelper.index, i] = newCh1;
                            ExperimentalSequencerThreadHelper.DoubleDataArray[1][ExperimentalSequencerThreadHelper.index, i] = newCh2;
                            ExperimentalSequencerThreadHelper.DoubleDataArray[2][ExperimentalSequencerThreadHelper.index, i] = newSum;
                            ExperimentalSequencerThreadHelper.DoubleDataArray[3][ExperimentalSequencerThreadHelper.index, i] = newDiff;
                            //New Error estimates
                            ExperimentalSequencerThreadHelper.DoubleDataArray[4][ExperimentalSequencerThreadHelper.index, i] = newCh1Err;
                            ExperimentalSequencerThreadHelper.DoubleDataArray[5][ExperimentalSequencerThreadHelper.index, i] = newCh2Err;
                            ExperimentalSequencerThreadHelper.DoubleDataArray[6][ExperimentalSequencerThreadHelper.index, i] = newSumErr;
                            ExperimentalSequencerThreadHelper.DoubleDataArray[7][ExperimentalSequencerThreadHelper.index, i] = newDiffErr;

                        }

                        //call to update intensity graphs
                        try
                        {
                            this.Invoke(new MyDelegateThreadHelper(ExpSeqIntensityGraphUpdateCallbackFn), ExperimentalSequencerThreadHelper);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        //update 1D plot
                        try
                        {
                            this.Invoke(new MyDelegateThreadHelper(ExpSeqViewScatterGraphUpdateCallbackFn), ExperimentalSequencerThreadHelper);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }

                    //index++
                    ExperimentalSequencerThreadHelper.index++;
                }
                if (ExperimentalSequencerThreadHelper.ShouldBeRunningFlag)
                {
                    //save Scan Data
                    SaveScanData(ExperimentalSequencerThreadHelper);
                }

                //Interlocked Scan 1
                if (ExpSeqScan1Checkbox.Checked && ((ExperimentalSequencerThreadHelper.index2+1)%int.Parse(ExpSeqIntScan1Period.Text)==0))
                {
                    InterlockedScan1ThreadHelper.index = 0;
                    //run scans
                    while (InterlockedScan1ThreadHelper.index < (InterlockedScan1ThreadHelper.numPoints) && InterlockedScan1ThreadHelper.ShouldBeRunningFlag)
                    {
                        //update label
                        updateInt = InterlockedScan1ThreadHelper.index + 1;
                        update = "Point # " + updateInt.ToString();
                        try
                        {
                            this.Invoke(new MyDelegateLabelUpdate(UpdateLabelCallbackFn), update, ExpSeqIntScan1ProgressLabel);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //Compute new field values
                        if (InterlockedScan1ThreadHelper.numPoints > 1) { InterlockedScan1ThreadHelper.DoubleScanVariable[0, InterlockedScan1ThreadHelper.index] = (double)(InterlockedScan1ThreadHelper.min[0] + (InterlockedScan1ThreadHelper.max[0] - InterlockedScan1ThreadHelper.min[0]) * InterlockedScan1ThreadHelper.index / (InterlockedScan1ThreadHelper.numPoints - 1)); }
                        else { InterlockedScan1ThreadHelper.DoubleScanVariable[0, InterlockedScan1ThreadHelper.index] = InterlockedScan1ThreadHelper.min[0]; }
                        
                        if (InterlockedScan1ThreadHelper.numScanVar > 1)
                        {
                            if (InterlockedScan1ThreadHelper.numPoints > 1) { InterlockedScan1ThreadHelper.DoubleScanVariable[1, InterlockedScan1ThreadHelper.index] = (double)(InterlockedScan1ThreadHelper.min[1] + (InterlockedScan1ThreadHelper.max[1] - InterlockedScan1ThreadHelper.min[1]) * InterlockedScan1ThreadHelper.index / (InterlockedScan1ThreadHelper.numPoints - 1)); }
                            else { InterlockedScan1ThreadHelper.DoubleScanVariable[1, InterlockedScan1ThreadHelper.index] = InterlockedScan1ThreadHelper.min[1]; }
                        }
                        //call to change field value
                        try
                        {
                            this.Invoke(new MyDelegateThreadHelper(ScanUpdateCallbackFn), InterlockedScan1ThreadHelper);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        //if first scan, pause for 500ms
                        if (InterlockedScan1ThreadHelper.index == 0)
                        {
                            //wait for 500ms before scanning
                            Thread.Sleep(int.Parse(ExpSeqTimeDelay.Text));
                        }

                        //get Data from selected Data Stream into ThreadHelper variable
                        //update graphs
                        getDatafromStream(InterlockedScan1ThreadHelper);
                        //index++
                        InterlockedScan1ThreadHelper.index++;
                    }
                    if (InterlockedScan1ThreadHelper.ShouldBeRunningFlag)
                    {
                        //save Scan Data
                        SaveScanData(InterlockedScan1ThreadHelper);
                    }
                    //go back to initial value
                    //reset button
                    try
                    {
                        this.Invoke(new MyDelegateThreadHelper(ScanResetCallbackFn), InterlockedScan1ThreadHelper);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    //reset scan boolean
                    InterlockedScan1ThreadHelper.ShouldBeRunningFlag = false;
                }

                //Interlocked Scan 2
                if (ExpSeqScan2Checkbox.Checked && ((ExperimentalSequencerThreadHelper.index2 + 1) % int.Parse(ExpSeqIntScan2Period.Text) == 0))
                {
                    InterlockedScan2ThreadHelper.index = 0;
                    //run scans
                    while (InterlockedScan2ThreadHelper.index < (InterlockedScan2ThreadHelper.numPoints) && InterlockedScan2ThreadHelper.ShouldBeRunningFlag)
                    {
                        //update label
                        updateInt = InterlockedScan2ThreadHelper.index + 1;
                        update = "Point # " + updateInt.ToString();
                        try
                        {
                            this.Invoke(new MyDelegateLabelUpdate(UpdateLabelCallbackFn), update, ExpSeqIntScan2ProgressLabel); 
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                        //Compute new field values
                        if (InterlockedScan2ThreadHelper.numPoints > 1) { InterlockedScan2ThreadHelper.DoubleScanVariable[0, InterlockedScan2ThreadHelper.index] = (double)(InterlockedScan2ThreadHelper.min[0] + (InterlockedScan2ThreadHelper.max[0] - InterlockedScan2ThreadHelper.min[0]) * InterlockedScan2ThreadHelper.index / (InterlockedScan2ThreadHelper.numPoints - 1)); }
                        else { InterlockedScan2ThreadHelper.DoubleScanVariable[0, InterlockedScan2ThreadHelper.index] = InterlockedScan2ThreadHelper.min[0]; }

                        if (InterlockedScan2ThreadHelper.numScanVar > 1)
                        {
                            InterlockedScan2ThreadHelper.DoubleScanVariable[1, InterlockedScan2ThreadHelper.index] = (double)(InterlockedScan2ThreadHelper.min[1] + (InterlockedScan2ThreadHelper.max[1] - InterlockedScan2ThreadHelper.min[1]) * InterlockedScan2ThreadHelper.index / (InterlockedScan2ThreadHelper.numPoints - 1));
                        }
                        //call to change field value
                        try
                        {
                            this.Invoke(new MyDelegateThreadHelper(ScanUpdateCallbackFn), InterlockedScan2ThreadHelper);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        //if first scan, pause for 500ms
                        if (InterlockedScan2ThreadHelper.index == 0)
                        {
                            //wait for 500ms before scanning
                            Thread.Sleep(int.Parse(ExpSeqTimeDelay.Text));
                        }

                        //get Data from selected Data Stream into ThreadHelper variable
                        //update graphs
                        getDatafromStream(InterlockedScan2ThreadHelper);

                        //Fluorescence Interrupt
                        if(ExpSeqFluorInterruptCheck.Checked)
                        {
                            if(InterlockedScan2ThreadHelper.DoubleData[0,InterlockedScan2ThreadHelper.index] < double.Parse(ExpSeqFluorInterruptThreshold.Text))
                            {
                                ExperimentalSequencerThreadHelper.ShouldBeRunningFlag = false;
                                MessageBox.Show("Experimental Sequencer Stopped at " + DateTime.Now.ToString("hhmmss"));
                            }
                        }

                        //index++
                        InterlockedScan2ThreadHelper.index++;
                    }
                    if (InterlockedScan2ThreadHelper.ShouldBeRunningFlag)
                    {
                        //save Scan Data
                        SaveScanData(InterlockedScan2ThreadHelper);
                    }
                    //go back to initial value
                    //reset button
                    try
                    {
                        this.Invoke(new MyDelegateThreadHelper(ScanResetCallbackFn), InterlockedScan2ThreadHelper);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    //reset scan boolean
                    InterlockedScan2ThreadHelper.ShouldBeRunningFlag = false;
                }

                //increase scan index
                ExperimentalSequencerThreadHelper.index2++;
            }
            //go back to initial value
            //reset button
            try
            {
                this.Invoke(new MyDelegateThreadHelper(ScanResetCallbackFn),ExperimentalSequencerThreadHelper);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            //reset scan boolean
            ExperimentalSequencerThreadHelper.ShouldBeRunningFlag = false;
        }

        private void ExpSeqPauseButton_Click(object sender, EventArgs e)
        {
            if (ExperimentalSequencerThreadHelper.ShouldBeRunningFlag && !(ExperimentalSequencerThreadHelper.theThread.ThreadState == System.Threading.ThreadState.Suspended))
            {
                ExperimentalSequencerThreadHelper.theThread.Suspend();
                ExpSeqPauseButton.Text = "RESUME";
                ExpSeqPauseButton.BackColor = System.Drawing.Color.White;
                ExpSeqStartButton.Enabled = false;
            }
            else if (ExperimentalSequencerThreadHelper.ShouldBeRunningFlag)
            {
                ExperimentalSequencerThreadHelper.theThread.Resume();
                ExpSeqPauseButton.Text = "Pause Sequencer";
                ExpSeqPauseButton.BackColor = System.Drawing.Color.WhiteSmoke;
                ExpSeqStartButton.Enabled = true;
            }
            
        }

        private void ExpSeqPresetScanBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (ExpSeqPresetScanBox.Text)
            {
                case "Bx Slider":
                    ExpSeqMainSlider1.Text = "BxSlider";
                    ExpSeqMainSlider2.Text = "None";
                    ExpSeqMainSliderStartText.Text = "0";
                    ExpSeqMainSliderEndText.Text = "2";
                    ExpSeqMainSliderNumPointsText.Text = "50";
                    ExpSeqMainSliderNumScansText.Text = "1";
                    ExpSeqMainData.Text = "Correlator:Sum";
                    ExpSeqScan1Checkbox.Checked = false;
                    ExpSeqScan2Checkbox.Checked = false;
                    break;
                case "Electrode Scan":
                    ExpSeqMainSlider1.Text = "DC2";
                    ExpSeqMainSlider2.Text = "DC9";
                    ExpSeqMainSliderStartText.Text = "12.4";
                    ExpSeqMainSliderEndText.Text = "11.6";
                    ExpSeqMainSlider2StartText.Text = "11.6";
                    ExpSeqMainSlider2EndText.Text = "12.4";
                    ExpSeqMainSliderNumPointsText.Text = "50";
                    ExpSeqMainSliderNumScansText.Text = "1";
                    ExpSeqMainData.Text = "Correlator:Sum";
                    ExpSeqScan1Checkbox.Checked = false;
                    ExpSeqScan2Checkbox.Checked = false;
                    break;
                case "Repumper Slider":
                    ExpSeqMainSlider1.Text = "RepumperSlider";
                    ExpSeqMainSlider2.Text = "None";
                    ExpSeqMainSliderStartText.Text = "2.85";
                    ExpSeqMainSliderEndText.Text = "3.15";
                    ExpSeqMainSliderNumPointsText.Text = "50";
                    ExpSeqMainSliderNumScansText.Text = "1";
                    ExpSeqMainData.Text = "Correlator:Sum";
                    ExpSeqScan1Checkbox.Checked = false;
                    ExpSeqScan2Checkbox.Checked = false;
                    break;
                case "DX Slider":
                    ExpSeqMainSlider1.Text = "DXSlider";
                    ExpSeqMainSlider2.Text = "None";
                    ExpSeqMainSliderStartText.Text = "4.5";
                    ExpSeqMainSliderEndText.Text = "5.5";
                    ExpSeqMainSliderNumPointsText.Text = "10";
                    ExpSeqMainSliderNumScansText.Text = "10";
                    ExpSeqMainData.Text = "Correlator:Channels";
                    ExpSeqScan1Checkbox.Checked = false;
                    ExpSeqScan2Checkbox.Checked = false;
                    break;
            }

        }


        

//////////////////////////////// IAN's Rigol Function Generator Control /////////////////////////

       //"Import" button click event
        //---------------------------
        //retrives the waveform info in the radio buttons and textboxes
        //creates a new ImportedWaveform object with this info and adds it to an ArrayList
        //---------------------------
        private void RigImport_Click(object sender, EventArgs e)
        {
            //handle unchecked radio buttons and unfilled text boxes
            if ((RigCh1Opt.Checked || RigCh2Opt.Checked) && !string.IsNullOrEmpty(RigFreqText.Text)
                && !string.IsNullOrEmpty(RigAmplText.Text) && !string.IsNullOrEmpty(RigOffsetText.Text)
                && !string.IsNullOrEmpty(RigPhaseText.Text) && !string.IsNullOrEmpty(RigBrowseText.Text))
            {
                //retrive waveform info
                int channel;
                if (RigCh1Opt.Checked)
                {
                    channel = Convert.ToInt16(RigCh1Opt.Text); //channel 1
                }
                else
                {
                    channel = Convert.ToInt16(RigCh2Opt.Text); //channel 2
                }

                //handle bad input
                try
                {
                    double frequency = Convert.ToDouble(RigFreqText.Text);
                    double amplitude = Convert.ToDouble(RigAmplText.Text);
                    double offset = Convert.ToDouble(RigOffsetText.Text);
                    double phase = Convert.ToDouble(RigPhaseText.Text);
                    string filename = RigBrowseText.Text;

                    //create new ImportedWaveform object
                    ImportedWaveform waveform =
                        new ImportedWaveform(channel, frequency, amplitude, offset, phase, filename);

                    //add it to the ArrayList importedWaveforms
                    importedWaveforms.Add(waveform);

                    //populate the listbox with a new ListItem
                    RigFileListBox.Items.Add(new ListItem(waveform.Display(), Convert.ToString(index)));
                    index++;
                }
                catch
                {
                    MessageBox.Show("Unexpected input format.");
                }
            }
        }

        //"Send" button click event
        //---------------------------
        //creates a new Rigol object (from Rigol class) using the FN GEN selection
        //uses the selected ImportedWaveform object to run the Rigol class's GenerateWaveform method
        //---------------------------
        private void RigProgramRigol_Click(object sender, EventArgs e)
        {
            //handle unchecked radio button and unselected waveforms
            if ((RigGen1.Checked || RigGen2.Checked) && RigFileListBox.SelectedIndex > -1)
            {
                //determine which fn gen to send to
                string usbID;
                if (RigGen1.Checked)
                {
                    usbID = "USB0::0x1AB1::0x0641::DG4C141600215::INSTR";
                }
                else
                {
                    usbID = "USB0::0x1AB1::0x0641::DG4C141400145::INSTR";
                }

                //create new Rigol object
                Rigol rigol = new Rigol(usbID);

                //determine which waveform to send
                int waveformIndex = Convert.ToInt16(((ListItem)RigFileListBox.SelectedItem).Value);
                ImportedWaveform waveform = (ImportedWaveform)importedWaveforms[waveformIndex];

                //handle bad filenames
                try
                {
                    //send it
                    rigol.GenerateWaveform(waveform.getChannel(), waveform.getFrequency(),
                        waveform.getAmplitude(), waveform.getOffset(), waveform.getPhase(),
                        waveform.getFilename());
                }
                catch
                {
                    MessageBox.Show("Couldn't find file... Or unexpected file contents... Or couldn't find RIGOL.");
                }
            }  
        }

        //"Browse" button click event
        //---------------------------
        //opens a file browser
        //---------------------------
        private void RigBrowseButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "txt files (*.txt)|*.txt";
            dialog.InitialDirectory = "\\\\Iondance\\experimental control";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                RigBrowseText.Text = dialog.FileName;
            }
        }

        private void browseA1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialogA1 = new OpenFileDialog();
            dialogA1.InitialDirectory = "F:\\VIs\\PULSE_SEQUENCER_AND_CORRELATOR\\PSEC_multichannel";
            dialogA1.RestoreDirectory = true;
            dialogA1.Filter = "bit files (*.bit)|*.bit";


            if (dialogA1.ShowDialog() == DialogResult.OK)
            {
                correlatorBitFilePath.Text = dialogA1.FileName;
            }
        }

        private void browseB1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialogB1 = new OpenFileDialog();
            dialogB1.Filter = "bit files (*.bit)|*.bit";
            dialogB1.InitialDirectory = "F:\\VIs\\PULSE_SEQUENCER_AND_CORRELATOR\\PSEC_multichannel";

            if (dialogB1.ShowDialog() == DialogResult.OK)
            {
                correlatorBitFilePathB.Text = dialogB1.FileName;
            }
        }

        private void browseA2_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialogA2 = new OpenFileDialog();
            dialogA2.Filter = "bit files (*.bit)|*.bit";
            dialogA2.InitialDirectory = "F:\\VIs\\PULSE_SEQUENCER_AND_CORRELATOR\\PSEC_multichannel";

            if (dialogA2.ShowDialog() == DialogResult.OK)
            {
                correlatorBitFilePath_manybins.Text = dialogA2.FileName;
            }
        }

        private void browseB2_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialogB2 = new OpenFileDialog();
            dialogB2.Filter = "bit files (*.bit)|*.bit";
            dialogB2.InitialDirectory = "F:\\VIs\\PULSE_SEQUENCER_AND_CORRELATOR\\PSEC_multichannel";

            if (dialogB2.ShowDialog() == DialogResult.OK)
            {
                correlatorBitFilePath_manybinsB.Text = dialogB2.FileName;
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "text files (*.txt)|*.txt";
            dialog.InitialDirectory = textBox6.Text;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = dialog.FileName;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "text files (*.txt)|*.txt";
            dialog.InitialDirectory = textBox6.Text;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox2.Text = dialog.FileName;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "text files (*.txt)|*.txt";
            dialog.InitialDirectory = textBox6.Text;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox3.Text = dialog.FileName;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "text files (*.txt)|*.txt";
            dialog.InitialDirectory = textBox6.Text;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox4.Text = dialog.FileName;
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "text files (*.txt)|*.txt";
            dialog.InitialDirectory = textBox6.Text;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox5.Text = dialog.FileName;
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.SelectedPath = textBox6.Text;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox6.Text = dialog.SelectedPath;
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            SavePulseSequencerSettings(textBox6.Text +"\\" + textBox7.Text);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            { ReadConfigurationFull(textBox1.Text); }
            else if (radioButton2.Checked)
            { ReadConfigurationFull(textBox2.Text); }
            else if (radioButton3.Checked)
            { ReadConfigurationFull(textBox3.Text); }
            else if (radioButton4.Checked)
            { ReadConfigurationFull(textBox4.Text); }
            else if (radioButton5.Checked)
            { ReadConfigurationFull(textBox5.Text); }
        }



    }


    //class: ImportedWaveform
    //-----------------------
    //represents the settings and filename of the imported waveform
    //-----------------------
    public class ImportedWaveform
    {

        //instance variables
        private int channel;
        private double frequency;
        private double amplitude;
        private double offset;
        private double phase;
        private string filename;

        //constructor
        public ImportedWaveform(int ch, double freq, double amp, double off, double ph, string file)
        {
            channel = ch;
            frequency = freq;
            amplitude = amp;
            offset = off;
            phase = ph;
            filename = file;
        }

        //getter methods
        public int getChannel()
        {
            return channel;
        }
        public double getFrequency()
        {
            return frequency;
        }
        public double getAmplitude()
        {
            return amplitude;
        }
        public double getOffset()
        {
            return offset;
        }
        public double getPhase()
        {
            return phase;
        }
        public string getFilename()
        {
            return filename;
        }

        //method: display waveform info
        public string Display()
        {
            return getChannel() + "_" + getFrequency() + "_" + getAmplitude() +
                "_" + getOffset() + "_" + getPhase() + "_" + getFilename();
        }
    }


////////////////////////////////////////////////

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

           
    

