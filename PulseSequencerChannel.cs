using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
//using System.Linq;

namespace ArrayDACControl
{
    
    public partial class PulseSequencerChannel : UserControl
    {
        const int wfmpts = 1000;
        
        double expPeriod;
        double param1;
        double param2;
        double param3;

        double param1opt1;
        double param2opt1;
        double param3opt1;
        double param1opt2;
        double param2opt2;
        double param3opt2;
        double param1opt3;
        double param2opt3;
        double param3opt3;

        double[] wfm = new double[wfmpts];
        
        Color chcolor;

        // Declare the event, which is associated with our
        // delegate SubmitClickedHandler(). Add some attributes
        // for the Visual C# control property.
        public delegate void ParamEntryHandler(object sender, EventArgs e);
        public delegate void OptionChangeHandler(object sender, EventArgs e);

        [Category("Action")]
        [Description("Fires when param values are changed in the textboxes.")]
        public event ParamEntryHandler paramsEntered;

        [Category("Action")]
        [Description("Fires when an option button has changed.")]
        public event OptionChangeHandler optionChanged;

        //Constructor
        public PulseSequencerChannel()
        {
            InitializeComponent();

            param1opt1 = double.Parse(SigParam1.Text);
            param2opt1 = double.Parse(SigParam2.Text);
            param3opt1 = double.Parse(SigParam3.Text);

            param1opt2 = 100;
            param2opt2 = 50;
            param3opt2 = 50;
            param1opt3 = 100;
            param2opt3 = 25;
            param3opt3 = 25;

            expPeriod = 200;

            sync();
        }

        //LABELS
        //Channel Label
        public String ChannelLabel
        {
            get {   return this.chlabel.Text;  }
            set {   this.chlabel.Text = value; }
        }
        //Input Label 1
        public String Param1Label
        {
            get { return this.inplabel1.Text;  }
            set { this.inplabel1.Text = value;  }
        }
        //Input Label 2
        public String Param2Label   
        {
            get { return this.inplabel2.Text;   }
            set { this.inplabel2.Text = value;   }
        }
        //Input Label 1
        public String Param3Label
        {
            get { return this.inplabel3.Text;}         
            set { this.inplabel3.Text = value;   }
        }
        //Option 2 Label
        public String Opt2Label
        {
            get { return this.opt2.Text; }
            set { this.opt2.Text = value; }
        }
        //Option 3 Label
        public String Opt3Label
        {
            get {   return this.opt3.Text;       }
            set {  this.opt3.Text = value;       }
        }
        //Channel Name
        public String Name
        {
            get {   return this.SigName.Text; }
            set {  this.SigName.Text = value; }
        }
        //Channel Color
        public Color ChannelColor
        {
            get { return chcolor; }
            set { 
                 chcolor = value;
                 this.chlabel.ForeColor = chcolor;
                 this.waveformGraph1.Plots[0].LineColor = chcolor;
            }
        } 

        //CHANNEL PARAMETERS////////////////////////////////

        //expPeriod
        public double ExpPeriodValue
        {
            get { return expPeriod; }
            set 
            { 
                expPeriod = value;
                sync();
            }

        }

        //Param1 value
        public double Param1Value
        {
            get { return param1; }
            set
            {
                param1 = value;
                directSet();
            }
        }

        public double Param1opt1Value
        {
            get { return param1opt1; }
            set
            {
               param1opt1 = value;
               sync();
            }
        }

        public double Param1opt2Value
        {
            get { return param1opt2; }
            set
            {
                param1opt2 = value;
                sync();
            }
        }

        public double Param1opt3Value
        {
            get { return param1opt3; }
            set
            {
                param1opt3 = value;
                sync();
            }
        }

        //Param2 value
        public double Param2Value
        {
            get { return param2; }
            set
            {
                param2 = value;
                directSet();
            }
        }

        public double Param2opt1Value
        {
            get { return param2opt1; }
            set
            {
                param2opt1 = value;
                sync();
            }
        }

        public double Param2opt2Value
        {
            get { return param2opt2; }
            set
            {
                param2opt2 = value;
                sync();
            }
        }

        public double Param2opt3Value
        {
            get { return param2opt3; }
            set
            {
                param2opt3 = value;
                sync();
            }
        }

        //Param3 value
        public double Param3Value
        {
            get { return param3; }
            set
            {
                param3 = value;
                directSet();
            }
        }

        public double Param3opt1Value
        {
            get { return param3opt1; }
            set
            {
                param3opt1 = value;
                sync();
            }
        }

        public double Param3opt2Value
        {
            get { return param3opt2; }
            set
            {
                param3opt2 = value;
                sync();
            }
        }

        public double Param3opt3Value
        {
            get { return param3opt3; }
            set
            {
                param3opt3 = value;
                sync();
            }
        }

        // option control:
        public bool opt1Value
        {
            get
            {
                return opt1.Checked;
            }
            set
            {
                this.opt1.Checked = value;
                sync();
            }
        }
        public bool opt2Value
        {
            get { return opt2.Checked; }
            set
            {
                this.opt2.Checked = value;
                sync();
            }
        }
        public bool opt3Value
        {
            get { return opt3.Checked; }
            set
            {
                this.opt3.Checked = value;
                sync();
            }
        }

        // Helper functions://///////////////////////////////////////////////////

        private void makeWfm()
        {
            int subperpts = Math.Max((int)(Math.Round(wfmpts * param1 / expPeriod,0)),1);  // number of sample points within one subperiod; there are wfmpts number of points in the overall period
            int sec1pts = (int)(Math.Round(wfmpts * param3 / expPeriod, 0));
            int sec2pts = (int)(Math.Round(wfmpts * param2 / expPeriod, 0));

            for (int i = 0; i < wfmpts; i++)
            {
                int subi = i%subperpts;
                subi = subi + 1;

                if ((subi > sec1pts) && (subi <= sec1pts + sec2pts))
                    wfm[i] = 1;
                else
                    wfm[i] = 0;
            }

            waveformGraph1.PlotY(wfm);            
        }

        private void sync()
        {
            if (opt1.Checked)
            {
                SigParam1.Enabled = true;
                SigParam2.Enabled = true;
                SigParam3.Enabled = true;
                SigParam1.BackColor = System.Drawing.Color.White;
                SigParam2.BackColor = System.Drawing.Color.White;
                SigParam3.BackColor = System.Drawing.Color.White;
                param1 = param1opt1;
                SigParam1.Text = param1.ToString();
                param2 = param2opt1;
                SigParam2.Text = param2.ToString();
                param3 = param3opt1;
                SigParam3.Text = param3.ToString();
            }
            else if (opt2.Checked)
            {
                SigParam1.Enabled = false;
                SigParam2.Enabled = false;
                SigParam3.Enabled = false;
                SigParam1.BackColor = System.Drawing.Color.LightGray;
                SigParam2.BackColor = System.Drawing.Color.LightGray;
                SigParam3.BackColor = System.Drawing.Color.LightGray;
                param1 = param1opt2;
                SigParam1.Text = param1.ToString();
                param2 = param2opt2;
                SigParam2.Text = param2.ToString();
                param3 = param3opt2;
                SigParam3.Text = param3.ToString();
            }
            else if (opt3.Checked)
            {
                SigParam1.Enabled = false;
                SigParam2.Enabled = false;
                SigParam3.Enabled = false;
                SigParam1.BackColor = System.Drawing.Color.LightGray;
                SigParam2.BackColor = System.Drawing.Color.LightGray;
                SigParam3.BackColor = System.Drawing.Color.LightGray;
                param1 = param1opt3;
                SigParam1.Text = param1.ToString();
                param2 = param2opt3;
                SigParam2.Text = param2.ToString();
                param3 = param3opt3;
                SigParam3.Text = param3.ToString();
            }

            makeWfm();
        }

        private void directSet()
        {
            SigParam1.Text = param1.ToString();
            SigParam2.Text = param2.ToString();
            SigParam3.Text = param3.ToString();

            if (opt1.Checked)
            {
                SigParam1.Enabled = true;
                SigParam2.Enabled = true;
                SigParam3.Enabled = true;
                SigParam1.BackColor = System.Drawing.Color.White;
                SigParam2.BackColor = System.Drawing.Color.White;
                SigParam3.BackColor = System.Drawing.Color.White;
                param1opt1 = param1;
                param2opt1 = param2;
                param3opt1 = param3;
            }
            else if (opt2.Checked)
            {
                SigParam1.Enabled = false;
                SigParam2.Enabled = false;
                SigParam3.Enabled = false;
                SigParam1.BackColor = System.Drawing.Color.LightGray;
                SigParam2.BackColor = System.Drawing.Color.LightGray;
                SigParam3.BackColor = System.Drawing.Color.LightGray;
                param1opt2 = param1;
                param2opt2 = param2;
                param3opt2 = param3;
            }
            else if (opt3.Checked)
            {
                SigParam1.Enabled = false;
                SigParam2.Enabled = false;
                SigParam3.Enabled = false;
                SigParam1.BackColor = System.Drawing.Color.LightGray;
                SigParam2.BackColor = System.Drawing.Color.LightGray;
                SigParam3.BackColor = System.Drawing.Color.LightGray;
                param1opt3 = param1;
                param2opt3 = param2;
                param3opt3 = param3;
            }

            makeWfm();
        }

        //Events: /////////////////////////////////////////////////////////////////

        // When SigParam1 input loses focus:
        private void SigParam1_Enter(object sender, EventArgs e)
        {
            try
            {
                param1opt1 = double.Parse(SigParam1.Text);
                sync();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            if (paramsEntered != null)
                optionChanged(sender, e);
        }
        // When SigParam2 input loses focus:
        private void SigParam2_Enter(object sender, EventArgs e)
        {
            try
            {
                param2opt1 = double.Parse(SigParam2.Text);
                sync();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            if (paramsEntered != null)
                optionChanged(sender, e);
        }
        // When SigParam3 input loses focus:
        private void SigParam3_Enter(object sender, EventArgs e)
        {
            try
            {
                param3opt1 = double.Parse(SigParam3.Text);
                sync();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            if (paramsEntered != null)
                optionChanged(sender, e);
        }

        private void opt1_CheckedChanged(object sender, EventArgs e)
        {
            sync();
            if (optionChanged != null)
                optionChanged(sender, e);
        }
        private void opt2_CheckedChanged(object sender, EventArgs e)
        {
            sync();
            if (optionChanged != null)
                optionChanged(sender, e);
        }
        private void opt3_CheckedChanged(object sender, EventArgs e)
        {
            sync();
            if (optionChanged != null)
                optionChanged(sender, e);
        }

    }
}
